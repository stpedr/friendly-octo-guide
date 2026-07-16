using Platform.Audit;

namespace Identity.Api;

/// <summary>
/// Sink de auditoria da FASE 0: emite o evento como log estruturado (Serilog →
/// OTel). before/after já vêm redigidos, então não vaza segredo — mas o log tem
/// retenção curta. A FASE 1 troca isto por um sink de outbox (na mesma transação
/// da ação) → tópico auditoria.admin.v1 → Data.Archiver → WORM, sem tocar no
/// IAdminAuditTrail nem em quem o chama.
/// </summary>
public sealed partial class LoggingAdminAuditTrail(ILogger<LoggingAdminAuditTrail> log) : IAdminAuditTrail
{
    public Task RecordAsync(AdminAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        LogAudited(
            auditEvent.EventId, auditEvent.Action, auditEvent.Actor,
            auditEvent.TargetType, auditEvent.TargetId,
            auditEvent.Before, auditEvent.After, auditEvent.TraceId);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "AUDIT {EventId} {Action} por {Actor} em {TargetType}:{TargetId} before=[{Before}] after=[{After}] trace={TraceId}")]
    private partial void LogAudited(
        Guid eventId, string action, string actor, string targetType, string targetId,
        string? before, string? after, string? traceId);
}
