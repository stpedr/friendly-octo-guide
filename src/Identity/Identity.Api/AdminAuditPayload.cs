using System.Text.Json;
using System.Text.Json.Serialization;
using Platform.Audit;

namespace Identity.Api;

/// <summary>
/// Serialização do evento de auditoria pro corpo JSONB do outbox — o contrato do
/// que vai pro tópico auditoria.admin.v1 e, por fim, pro lake WORM. Puro e
/// determinístico. before/after já vêm redigidos do Platform.Audit; aqui só se
/// carimba o formato.
/// </summary>
public static class AdminAuditPayload
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(AdminAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        return JsonSerializer.Serialize(new
        {
            eventId = auditEvent.EventId,
            actor = auditEvent.Actor,
            actorRoles = auditEvent.ActorRoles,
            action = auditEvent.Action,
            targetType = auditEvent.TargetType,
            targetId = auditEvent.TargetId,
            before = auditEvent.Before,
            after = auditEvent.After,
            traceId = auditEvent.TraceId,
            occurredAt = auditEvent.OccurredAt,
        }, Options);
    }
}
