using Platform.AccessControl;

namespace Chatbot.Domain.Tools;

public enum ToolKind
{
    Read,           // consulta: livre dentro do RBAC
    DestructiveAct, // muda estado do sistema ou da linha física: always_ask
}

/// <summary>
/// Ferramenta tipada que um agente pode chamar — o MESMO contrato auditado da API,
/// nunca acesso direto ao banco. O RouteRequirement é o mesmo do Gateway.
/// </summary>
public sealed record ToolDefinition(string Name, ToolKind Kind, RouteRequirement Requirement);

public enum ToolAccess
{
    Allowed,
    NeedsHumanConfirmation, // permission policy always_ask: ação só com humano confirmando
    DeniedByRbac,
    UnknownTool,
}

/// <summary>
/// Guardrail de agente: ação = ferramenta, e ferramenta passa por DOIS filtros —
/// RBAC do usuário logado (o agente nunca pode mais que o dono da sessão) e
/// natureza da ação (destrutiva/física exige confirmação humana, sempre).
/// </summary>
public sealed class ToolGuardrails(IReadOnlyDictionary<string, ToolDefinition> tools)
{
    public ToolAccess Evaluate(string toolName, Subject subject, bool humanConfirmed)
    {
        if (!tools.TryGetValue(toolName, out var tool))
            return ToolAccess.UnknownTool; // agente só age pelo que está registrado

        if (AccessPolicy.Evaluate(subject, tool.Requirement) != AccessDecision.Allow)
            return ToolAccess.DeniedByRbac;

        return tool.Kind == ToolKind.DestructiveAct && !humanConfirmed
            ? ToolAccess.NeedsHumanConfirmation
            : ToolAccess.Allowed;
    }
}
