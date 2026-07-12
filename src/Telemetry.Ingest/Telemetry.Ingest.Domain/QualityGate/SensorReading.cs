namespace Telemetry.Ingest.Domain.QualityGate;

/// <summary>Leitura crua vinda do tópico Kafka, já desserializada do Avro.</summary>
public sealed record SensorReading(
    string SensorId,
    double Value,
    DateTimeOffset MeasuredAt,
    DateTimeOffset ReceivedAt);
