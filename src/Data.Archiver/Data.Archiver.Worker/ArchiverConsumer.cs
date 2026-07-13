using System.Text.Json;
using Confluent.Kafka;
using Data.Archiver.Domain.Batching;
using Data.Archiver.Domain.Partitioning;
using Platform.Contracts;

namespace Data.Archiver.Worker;

/// <summary>
/// Kafka → data lake (S3/MinIO). Lote por partição, com estado do lote preso ao
/// primeiro offset: a chave do objeto é determinística, então replay sobrescreve
/// em vez de duplicar. Offset só é commitado DEPOIS do PutObject confirmado —
/// nunca existe leitura "arquivada" que não está no lake.
/// Payload que não decodifica não se perde: entra na mesma linha JSONL, cru em base64.
/// </summary>
public sealed partial class ArchiverConsumer(
    ArchiverOptions options,
    S3ObjectStore store,
    ILogger<ArchiverConsumer> log) : BackgroundService
{
    private sealed record PartitionBatch(ArchiveBatcher Batcher, long FirstOffset, DateTimeOffset FirstTimestamp)
    {
        public long LastOffset { get; set; }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await store.EnsureBucketAsync(stoppingToken);

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = options.KafkaBootstrap,
            GroupId = ArchiverTelemetry.ServiceName,
            EnableAutoCommit = false, // commit manual, só após o PutObject
            AutoOffsetReset = AutoOffsetReset.Earliest,
        };

        using var consumer = new ConsumerBuilder<string, byte[]>(consumerConfig).Build();
        consumer.Subscribe(options.Topic);
        LogConsuming(options.Topic, options.Bucket);

        var batches = new Dictionary<int, PartitionBatch>();

        while (!stoppingToken.IsCancellationRequested)
        {
            // Timeout curto: com o tópico parado, o lote ainda fecha por idade.
            var result = consumer.Consume(TimeSpan.FromSeconds(1));
            var now = DateTimeOffset.UtcNow;

            if (result is not null)
            {
                var partition = result.Partition.Value;
                if (!batches.TryGetValue(partition, out var batch))
                {
                    batch = new PartitionBatch(
                        new ArchiveBatcher(options.MaxRecordsPerObject, options.MaxBytesPerObject, options.MaxBatchAge),
                        result.Offset.Value, now);
                    batches[partition] = batch;
                }

                batch.Batcher.Add(ToJsonLine(result), now);
                batch.LastOffset = result.Offset.Value;
            }

            foreach (var (partition, batch) in batches.Where(kv => kv.Value.Batcher.ShouldFlush(now)).ToList())
            {
                var key = ArchivePartitioner.ObjectKey(options.Topic, partition, batch.FirstOffset, batch.FirstTimestamp);
                var lines = batch.Batcher.Drain();
                var bytes = await store.PutGzipJsonLinesAsync(key, lines, stoppingToken);

                consumer.Commit([new TopicPartitionOffset(options.Topic, partition, batch.LastOffset + 1)]);
                batches.Remove(partition);

                ArchiverTelemetry.Records.Add(lines.Count);
                ArchiverTelemetry.Objects.Add(1);
                ArchiverTelemetry.Bytes.Add(bytes);
                LogArchived(key, lines.Count, bytes);
            }
        }
    }

    // Linha JSONL: decodificada quando o codec entende, crua (base64) quando não —
    // o lake guarda TUDO que passou pelo tópico, inclusive o que a ingestão rejeitaria.
    private static string ToJsonLine(ConsumeResult<string, byte[]> result)
    {
        try
        {
            var record = SensorReadingCodec.Decode(result.Message.Value);
            return JsonSerializer.Serialize(new
            {
                sensorId = record.SensorId,
                value = record.Value,
                measuredAt = record.MeasuredAt,
                kafkaOffset = result.Offset.Value,
            });
        }
        catch (FormatException)
        {
            return JsonSerializer.Serialize(new
            {
                malformed = true,
                rawBase64 = Convert.ToBase64String(result.Message.Value),
                kafkaOffset = result.Offset.Value,
            });
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Arquivando {Topic} no bucket {Bucket}")]
    private partial void LogConsuming(string topic, string bucket);

    [LoggerMessage(Level = LogLevel.Information, Message = "Objeto {Key} gravado: {Count} leituras, {Bytes} bytes")]
    private partial void LogArchived(string key, int count, long bytes);
}
