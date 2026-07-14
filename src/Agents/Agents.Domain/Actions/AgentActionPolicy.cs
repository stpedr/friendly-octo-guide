namespace Agents.Domain.Actions;

public enum ActionKind
{
    Read,        // consultar/correlacionar: livre dentro do RBAC
    OpenTicket,  // registrar incidente: efeito colateral leve, permitido
    Corrective,  // muda estado de sistema (reprocessar, reiniciar serviço): always_ask
    Physical,    // comando de volta pra linha: always_ask + passa pelo Decision Engine
}

public enum ActionVerdict
{
    Proceed,
    NeedsHumanConfirmation,
    RoutedToDecisionEngine, // ação física NUNCA sai direto do agente — vai pro loop auditado
}

/// <summary>
/// Guardrail do agente autônomo, em código: leitura e abertura de ticket seguem
/// direto; ação corretiva exige confirmação humana (permission policy always_ask);
/// ação física jamais é executada pelo agente — é encaminhada ao Decision Engine,
/// que tem envelope de operação e aprovação por criticidade. O agente propõe; quem
/// mexe na linha é o canal auditado.
/// </summary>
public static class AgentActionPolicy
{
    public static ActionVerdict Evaluate(ActionKind kind, bool humanConfirmed) => kind switch
    {
        ActionKind.Read or ActionKind.OpenTicket => ActionVerdict.Proceed,
        ActionKind.Corrective => humanConfirmed ? ActionVerdict.Proceed : ActionVerdict.NeedsHumanConfirmation,
        ActionKind.Physical => ActionVerdict.RoutedToDecisionEngine,
        _ => ActionVerdict.NeedsHumanConfirmation,
    };
}
