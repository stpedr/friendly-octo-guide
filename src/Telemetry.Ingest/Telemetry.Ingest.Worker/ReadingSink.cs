using Confluent.Kafka;
using Npgsql;
using Telemetry.Ingest.Domain.QualityGate;

namespace Telemetry.Ingest.Worker;

/// <summary>
/// Destinos da leitura: Postgres (aceitas, idempotente) e tópico de quarentena
/// (rejeitadas, com o motivo em header — payload original intacto pra replay).
/// </summary>
public sealed class ReadingSink : IDisposable
{
    private readonly IngestOptions _options;
    private readonly IProducer<string, byte[]> _quarantineProducer;

    public ReadingSink(IngestOptions options)
    {
        _options = options;
        _quarantineProducer = new ProducerBuilder<string, byte[]>(new ProducerConfig
        {
            BootstrapServers = options.KafkaBootstrap,
            EnableIdempotence = true,
        }).Build();
    }

    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_options.PostgresConnection);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            CREATE TABLE IF NOT EXISTS telemetria (
                sensor_id TEXT NOT NULL,
                measured_at TIMESTAMPTZ NOT NULL,
                value DOUBLE PRECISION NOT NULL,
                received_at TIMESTAMPTZ NOT NULL,
                PRIMARY KEY (sensor_id, measured_at)
            );
            """, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Idempotente por (sensor_id, measured_at): replay do Kafka nunca duplica linha.</summary>
    public async Task WriteAsync(SensorReading reading, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_options.PostgresConnection);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO telemetria (sensor_id, measured_at, value, received_at)
            VALUES ($1, $2, $3, $4)
            ON CONFLICT (sensor_id, measured_at) DO NOTHING
            """, conn);
        cmd.Parameters.AddWithValue(reading.SensorId);
        cmd.Parameters.AddWithValue(reading.MeasuredAt);
        cmd.Parameters.AddWithValue(reading.Value);
        cmd.Parameters.AddWithValue(reading.ReceivedAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Rejeitada NUNCA é descartada: vai pro tópico de quarentena com o motivo no header.</summary>
    public async Task QuarantineAsync(Message<string, byte[]> original, string reason, CancellationToken ct)
    {
        var message = new Message<string, byte[]>
        {
            Key = original.Key,
            Value = original.Value, // payload intacto — replay pós-correção é possível
            Headers = [new Header("reason", System.Text.Encoding.UTF8.GetBytes(reason))],
        };
        await _quarantineProducer.ProduceAsync(_options.QuarantineTopic, message, ct);
    }

    public void Dispose() => _quarantineProducer.Dispose();
}
