using System.Diagnostics.CodeAnalysis;

namespace Platform.Audit;

/// <summary>
/// Linha do outbox de auditoria: o evento como foi gravado na mesma operação da
/// ação. PublishedAt nulo = ainda não chegou ao Kafka. DTO puro, fora da cobertura.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record AuditOutboxMessage(
    Guid Id,
    string Topic,
    string Payload,
    DateTimeOffset OccurredAt,
    DateTimeOffset? PublishedAt,
    int Attempts);

/// <summary>
/// Política do relay que drena o outbox de auditoria pro Kafka. Pura: decide O QUE
/// publicar e QUANDO reter; quem faz IO é o host. Backoff exponencial com teto —
/// evento de auditoria NUNCA é descartado; no pior caso fica pra trás e a fila
/// crescendo vira alerta. Mesma disciplina do outbox do Core.Execution.
/// </summary>
public static class AuditOutboxPolicy
{
    public static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(5);

    /// <summary>Backoff: 0 tentativas = já, N tentativas = 2^N * base, teto de 5 min.</summary>
    public static DateTimeOffset NextAttemptAt(AuditOutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Attempts == 0)
            return message.OccurredAt;

        var delay = TimeSpan.FromTicks(BaseDelay.Ticks * (1L << Math.Min(message.Attempts, 20)));
        return message.OccurredAt + (delay > MaxDelay ? MaxDelay : delay);
    }

    /// <summary>Lote a publicar agora, mais antigo primeiro — ordem de ocorrência é a ordem de publicação.</summary>
    public static IReadOnlyList<AuditOutboxMessage> DueBatch(
        IEnumerable<AuditOutboxMessage> pending, DateTimeOffset now, int batchSize)
    {
        ArgumentNullException.ThrowIfNull(pending);
        return [.. pending
            .Where(m => m.PublishedAt is null && NextAttemptAt(m) <= now)
            .OrderBy(m => m.OccurredAt)
            .Take(batchSize)];
    }
}
