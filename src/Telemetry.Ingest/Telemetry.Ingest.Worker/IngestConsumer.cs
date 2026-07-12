using Confluent.Kafka;
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
    ILogger<IngestConsumer> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Limites viriam do serviço de cadastro de sensores; fixo por enquanto (fase 1 troca por cache do Postgres).
        var gate = new Gate(
            limitsBySensor: new Dictionary<string, SensorLimits> { ["temp-forno-01"] = new(-40, 900) },
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

            var reading = AvroCodec.Decode(result.Message.Value, receivedAt: DateTimeOffset.UtcNow);
            activity?.SetTag("sensor.id", reading.SensorId);
            IngestTelemetry.LagSeconds.Record((reading.ReceivedAt - reading.MeasuredAt).TotalSeconds);

            var verdict = gate.Evaluate(reading);
            if (verdict.Accepted)
            {
                await Sink.WriteAsync(options.PostgresConnection, reading, stoppingToken); // idempotente (invariante 2)
                IngestTelemetry.Accepted.Add(1);
            }
            else
            {
                await Sink.QuarantineAsync(options, result.Message, verdict.Reason, stoppingToken); // invariante 1
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

// Stubs de infra — implementação real entra na semana 1 da fase 1.
// Mantidos fora do Domain de propósito: a lógica testável não conhece Kafka nem SQL.
internal static class AvroCodec
{
    public static SensorReading Decode(byte[] payload, DateTimeOffset receivedAt) =>
        throw new NotImplementedException("Desserialização Avro via Schema Registry — schemas/sensor-reading.avsc");
}

internal static class Sink
{
    public static Task WriteAsync(string connectionString, SensorReading reading, CancellationToken ct) =>
        throw new NotImplementedException(
            "INSERT INTO telemetria (sensor_id, measured_at, value) VALUES (...) ON CONFLICT DO NOTHING");

    public static Task QuarantineAsync(IngestOptions options, Message<string, byte[]> original,
        RejectionReason reason, CancellationToken ct) =>
        throw new NotImplementedException("Produce no tópico de quarentena com header 'reason'");
}
