using Confluent.Kafka;
using Platform.Contracts;
using Telemetry.Ingest.Domain.QualityGate;
using Gate = Telemetry.Ingest.Domain.QualityGate.QualityGate;

namespace Telemetry.Ingest.Worker;

/// <summary>
/// Loop de consumo: Kafka → QualityGate → Postgres (aceitas) / tópico de quarentena (rejeitadas).
/// Invariantes que este consumer garante:
///   1. Nunca perde: rejeitada vai pra quarentena, não pro lixo.
///   2. Nunca duplica: escrita idempotente por (sensor_id, measured_at) — ON CONFLICT DO NOTHING.
///   3. Commit de offset só DEPOIS da escrita confirmada (at-least-once + idempotência = efetivamente once).
/// </summary>
public sealed partial class IngestConsumer(
    IngestOptions options,
    ReadingSink sink,
    ILogger<IngestConsumer> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await sink.EnsureSchemaAsync(stoppingToken);

        // Limites viriam do serviço de cadastro de sensores; fixo por enquanto (fase 1 troca por cache do Postgres).
        var gate = new Gate(
            limitsBySensor: new Dictionary<string, SensorLimits>
            {
                ["temp-forno-01"] = new(-40, 900),
                ["pressao-linha-02"] = new(0, 400),
            },
            maxClockDrift: TimeSpan.FromSeconds(5),
            maxStaleness: TimeSpan.FromMinutes(10));

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = options.KafkaBootstrap,
            GroupId = IngestTelemetry.ServiceName,
            EnableAutoCommit = false,                    // invariante 3
            AutoOffsetReset = AutoOffsetReset.Earliest,  // replay reconstrói tudo
        };

        using var consumer = new ConsumerBuilder<string, byte[]>(consumerConfig).Build();
        consumer.Subscribe(options.TelemetryTopic);
        LogConsuming(options.TelemetryTopic, options.KafkaBootstrap);

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = consumer.Consume(stoppingToken);
            using var activity = IngestTelemetry.Activity.StartActivity("ingest.reading");

            SensorReading reading;
            try
            {
                var record = SensorReadingCodec.Decode(result.Message.Value);
                reading = new SensorReading(
                    record.SensorId, record.Value, record.MeasuredAt, DateTimeOffset.UtcNow,
                    ClockSourceMap.FromWire(record.ClockSource));
            }
            catch (FormatException)
            {
                // Payload que nem decodifica também não se perde — quarentena com motivo próprio.
                await sink.QuarantineAsync(result.Message, "MalformedPayload", stoppingToken);
                IngestTelemetry.Quarantined.Add(1, new KeyValuePair<string, object?>("reason", "MalformedPayload"));
                consumer.Commit(result);
                continue;
            }

            activity?.SetTag("sensor.id", reading.SensorId);
            IngestTelemetry.LagSeconds.Record((reading.ReceivedAt - reading.MeasuredAt).TotalSeconds);

            var verdict = gate.Evaluate(reading);
            if (verdict.Accepted)
            {
                await sink.WriteAsync(reading, stoppingToken); // idempotente (invariante 2)
                IngestTelemetry.Accepted.Add(1);
            }
            else
            {
                await sink.QuarantineAsync(result.Message, verdict.Reason.ToString(), stoppingToken); // invariante 1
                IngestTelemetry.Quarantined.Add(1, new KeyValuePair<string, object?>("reason", verdict.Reason));
                LogQuarantined(reading.SensorId, verdict.Reason);
            }

            consumer.Commit(result); // invariante 3: só após persistir
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Consumindo {Topic} em {Bootstrap}")]
    private partial void LogConsuming(string topic, string bootstrap);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Leitura em quarentena: {Sensor} · {Reason}")]
    private partial void LogQuarantined(string sensor, RejectionReason reason);
}
