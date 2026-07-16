using System.Diagnostics.CodeAnalysis;

namespace Platform.Audit;

/// <summary>
/// Espelho de schemas/auditoria-admin.avsc — auditoria.admin.v1.
/// Registro imutável: uma vez construído não muda (append-only por construção).
/// before/after já entram REDIGIDOS — segredo nunca chega aqui (ver AuditRedaction).
/// DTO puro sem lógica: fora da conta de cobertura (a barra mede a lógica de
/// redação/diff, não o carimbo de dados).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record AdminAuditEvent(
    Guid EventId,
    string Actor,
    IReadOnlyList<string> ActorRoles,
    string Action,
    string TargetType,
    string TargetId,
    string? Before,
    string? After,
    string? TraceId,
    DateTimeOffset OccurredAt);

/// <summary>
/// Vocabulário fechado de ações auditáveis — string no schema, constante aqui pra
/// que emissor e correlação (SIEM) falem o mesmo nome. Tipo novo entra AQUI primeiro.
/// </summary>
public static class AdminAction
{
    public const string PermissionChanged = "PermissionChanged";
    public const string DeviceCertRevoked = "DeviceCertRevoked";
    public const string DecisionOverride = "DecisionOverride";
}

/// <summary>Tipos de alvo de uma ação administrativa.</summary>
public static class AuditTargetType
{
    public const string User = "User";
    public const string Device = "Device";
    public const string Decision = "Decision";
}
