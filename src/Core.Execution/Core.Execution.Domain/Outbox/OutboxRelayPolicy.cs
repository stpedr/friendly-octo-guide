namespace Core.Execution.Domain.Outbox;

/// <summary>
/// Linha do outbox: o evento como foi gravado na mesma transação do agregado.
/// PublishedAt nulo = ainda não chegou ao Kafka.
/// </summary>
public sealed record OutboxMessage(
    Guid Id,
    string Topic,
    string Payload,
    DateTimeOffset OccurredAt,
    DateTimeOffset? PublishedAt,
    int Attempts);

/// <summary>
/// Política do relay que drena o outbox pro Kafka. Pura: decide O QUE publicar
/// e QUANDO reter; quem faz IO é o host. Backoff exponencial com teto — evento
/// nunca é descartado, no pior caso fica para trás e vira alerta de fila crescendo.
/// </summary>
public static class OutboxRelayPolicy
{
    public static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(5);

    /// <summary>Backoff: 0 tentativas = já, N tentativas = 2^N * base, teto de 5 min.</summary>
    public static DateTimeOffset NextAttemptAt(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Attempts == 0)
            return message.OccurredAt;

        var delay = TimeSpan.FromTicks(BaseDelay.Ticks * (1L << Math.Min(message.Attempts, 20)));
        return message.OccurredAt + (delay > MaxDelay ? MaxDelay : delay);
    }

    /// <summary>Lote a publicar agora, mais antigo primeiro — ordem de ocorrência é a ordem de publicação.</summary>
    public static IReadOnlyList<OutboxMessage> DueBatch(
        IEnumerable<OutboxMessage> pending, DateTimeOffset now, int batchSize)
    {
        ArgumentNullException.ThrowIfNull(pending);
        return [.. pending
            .Where(m => m.PublishedAt is null && NextAttemptAt(m) <= now)
            .OrderBy(m => m.OccurredAt)
            .Take(batchSize)];
    }
}
