using System.Text.Json;
using Core.Execution.Domain.Outbox;
using Core.Execution.Domain.WorkOrders;
using Npgsql;

namespace Core.Execution.Api;

/// <summary>
/// Persistência do agregado + outbox. A invariante que este store garante:
/// estado da ordem e evento correspondente entram na MESMA transação —
/// nunca existe escrita que grava no banco mas perde o evento (ou vice-versa).
/// </summary>
public sealed class WorkOrderStore(string connectionString)
{
    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            CREATE TABLE IF NOT EXISTS work_orders (
                id UUID PRIMARY KEY,
                line TEXT NOT NULL,
                product TEXT NOT NULL,
                quantity INT NOT NULL,
                state TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS outbox (
                id UUID PRIMARY KEY,
                topic TEXT NOT NULL,
                payload JSONB NOT NULL,
                occurred_at TIMESTAMPTZ NOT NULL,
                published_at TIMESTAMPTZ,
                attempts INT NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS outbox_pending ON outbox (occurred_at) WHERE published_at IS NULL;
            """, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task InsertAsync(WorkOrder order, WorkOrderEvent? evt, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var cmd = new NpgsqlCommand(
            "INSERT INTO work_orders (id, line, product, quantity, state) VALUES ($1, $2, $3, $4, $5)", conn, tx))
        {
            cmd.Parameters.AddWithValue(order.Id);
            cmd.Parameters.AddWithValue(order.Line);
            cmd.Parameters.AddWithValue(order.Product);
            cmd.Parameters.AddWithValue(order.Quantity);
            cmd.Parameters.AddWithValue(order.State.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        }

        if (evt is not null)
            await InsertOutboxAsync(conn, tx, order, evt, ct);

        await tx.CommitAsync(ct);
    }

    /// <summary>Aplica uma transição já validada pelo domínio: UPDATE + outbox, uma transação.</summary>
    public async Task ApplyTransitionAsync(WorkOrder order, WorkOrderEvent evt, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var cmd = new NpgsqlCommand(
            "UPDATE work_orders SET state = $2 WHERE id = $1", conn, tx))
        {
            cmd.Parameters.AddWithValue(order.Id);
            cmd.Parameters.AddWithValue(order.State.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await InsertOutboxAsync(conn, tx, order, evt, ct);
        await tx.CommitAsync(ct);
    }

    public async Task<WorkOrder?> FindAsync(Guid id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT id, line, product, quantity, state FROM work_orders WHERE id = $1", conn);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return Rehydrate(
            reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
            reader.GetInt32(3), Enum.Parse<WorkOrderState>(reader.GetString(4)));
    }

    public async Task<IReadOnlyList<OutboxMessage>> PendingOutboxAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT id, topic, payload::text, occurred_at, published_at, attempts FROM outbox WHERE published_at IS NULL", conn);

        var pending = new List<OutboxMessage>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            pending.Add(new OutboxMessage(
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
            "UPDATE outbox SET published_at = $2 WHERE id = $1", conn);
        cmd.Parameters.AddWithValue(messageId);
        cmd.Parameters.AddWithValue(publishedAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RecordAttemptAsync(Guid messageId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "UPDATE outbox SET attempts = attempts + 1 WHERE id = $1", conn);
        cmd.Parameters.AddWithValue(messageId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertOutboxAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, WorkOrder order, WorkOrderEvent evt, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            orderId = evt.OrderId,
            type = evt.Type,
            occurredAt = evt.OccurredAt,
            line = order.Line,
            state = order.State.ToString(),
        });

        await using var cmd = new NpgsqlCommand(
            "INSERT INTO outbox (id, topic, payload, occurred_at) VALUES ($1, $2, $3::jsonb, $4)", conn, tx);
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue("core.eventos.v1");
        cmd.Parameters.AddWithValue(payload);
        cmd.Parameters.AddWithValue(evt.OccurredAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // Reidratação sem passar pelas validações de criação: o banco é a verdade.
    private static WorkOrder Rehydrate(Guid id, string line, string product, int quantity, WorkOrderState state)
    {
        var order = new WorkOrder(id, line, product, quantity);
        var now = DateTimeOffset.UtcNow;
        // Reaplica transições até alcançar o estado persistido.
        if (state is WorkOrderState.Released or WorkOrderState.InProgress or WorkOrderState.Completed)
            order.Release(now);
        if (state is WorkOrderState.InProgress or WorkOrderState.Completed)
            order.Start(now);
        if (state is WorkOrderState.Completed)
            order.Complete(now);
        if (state is WorkOrderState.Aborted)
            order.Abort(now);
        return order;
    }
}
