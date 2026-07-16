using Npgsql;
using Platform.Audit;

namespace Identity.Api;

/// <summary>
/// Persistência append-only da trilha administrativa via outbox. O evento é
/// gravado durável ANTES da resposta ao admin (o store de usuários da prod é
/// externo — Keycloak — então não há transação a compartilhar; durável-antes-do-ack
/// é o equivalente correto à "mesma transação" do outbox do Core.Execution).
/// Um relay drena depois pro Kafka. Mesma tabela/índice do outbox do Core.
/// </summary>
public sealed class AdminAuditStore(string connectionString)
{
    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            CREATE TABLE IF NOT EXISTS admin_audit_outbox (
                id UUID PRIMARY KEY,
                topic TEXT NOT NULL,
                payload JSONB NOT NULL,
                occurred_at TIMESTAMPTZ NOT NULL,
                published_at TIMESTAMPTZ,
                attempts INT NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS admin_audit_outbox_pending ON admin_audit_outbox (occurred_at) WHERE published_at IS NULL;
            """, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task AppendAsync(AdminAuditEvent auditEvent, string topic, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO admin_audit_outbox (id, topic, payload, occurred_at) VALUES ($1, $2, $3::jsonb, $4)", conn);
        cmd.Parameters.AddWithValue(auditEvent.EventId);
        cmd.Parameters.AddWithValue(topic);
        cmd.Parameters.AddWithValue(AdminAuditPayload.Serialize(auditEvent));
        cmd.Parameters.AddWithValue(auditEvent.OccurredAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<AuditOutboxMessage>> PendingAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT id, topic, payload::text, occurred_at, published_at, attempts FROM admin_audit_outbox WHERE published_at IS NULL", conn);

        var pending = new List<AuditOutboxMessage>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            pending.Add(new AuditOutboxMessage(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                await reader.IsDBNullAsync(4, ct) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetInt32(5)));
        }
        return pending;
    }

    public async Task MarkPublishedAsync(Guid messageId, DateTimeOffset publishedAt, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "UPDATE admin_audit_outbox SET published_at = $2 WHERE id = $1", conn);
        cmd.Parameters.AddWithValue(messageId);
        cmd.Parameters.AddWithValue(publishedAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RecordAttemptAsync(Guid messageId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "UPDATE admin_audit_outbox SET attempts = attempts + 1 WHERE id = $1", conn);
        cmd.Parameters.AddWithValue(messageId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
