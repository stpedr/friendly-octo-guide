using Confluent.Kafka;
using Edge.ProtocolGateway.Domain.Buffering;
using Platform.Contracts;
using Platform.ServiceDefaults;

namespace Edge.ProtocolGateway.Worker;

/// <summary>
/// Lado IT: drena o buffer pro Kafka em lotes, no formato Avro do contrato
/// (schemas/sensor-reading.avsc). Se o produce falhar, o lote volta pro buffer —
/// store-and-forward de verdade: WAN caída retém, reconexão drena, nada some.
/// </summary>
public sealed partial class KafkaEgress(
    StoreAndForwardBuffer buffer,
    ServiceInstrumentation instrumentation,
    IConfiguration config,
    ILogger<KafkaEgress> log) : BackgroundService
{
    private const int BatchSize = 500;
    private static readonly TimeSpan DrainInterval = TimeSpan.FromMilliseconds(250);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config["Kafka:Bootstrap"] ?? "localhost:9092",
            EnableIdempotence = true,
        };
        using var producer = new ProducerBuilder<string, byte[]>(producerConfig).Build();

        var topic = config["Kafka:TelemetryTopic"] ?? "linha.telemetria.v1";
        var forwarded = instrumentation.Meter.CreateCounter<long>("edge.readings.forwarded");
        var bufferDepth = instrumentation.Meter.CreateGauge<long>("edge.buffer.depth");

        while (!stoppingToken.IsCancellationRequested)
        {
            bufferDepth.Record(buffer.Count);

            foreach (var evt in buffer.DrainBatch(BatchSize))
            {
                var payload = SensorReadingCodec.Encode(
                    new SensorReadingRecord(evt.SensorId, evt.Value, evt.MeasuredAt));
                try
                {
                    // Chave = sensor: preserva ordem por sensor na partição.
                    await producer.ProduceAsync(topic,
                        new Message<string, byte[]> { Key = evt.SensorId, Value = payload }, stoppingToken);
                    forwarded.Add(1);
                }
                catch (ProduceException<string, byte[]>)
                {
                    // WAN/broker fora: devolve pro buffer e espera o próximo ciclo.
                    if (!buffer.TryEnqueue(evt))
                        LogLostOnRequeue(evt.SensorId); // buffer encheu no meio-tempo — única perda possível, e é gritada
                    break;
                }
            }

            await Task.Delay(DrainInterval, stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Evento de {SensorId} perdido no requeue — buffer saturou durante indisponibilidade do Kafka")]
    private partial void LogLostOnRequeue(string sensorId);
}
