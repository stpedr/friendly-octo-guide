namespace Decision.Engine.Domain.Guardrails;

public enum Criticality { Low, Medium, High }

public enum DecisionOutcome
{
    AutoApproved,      // dentro do envelope + criticidade baixa → executa
    NeedsHumanApproval,// dentro do envelope, mas criticidade exige gente no loop
    Rejected,          // fora do envelope: nem chega a um humano — é fisicamente inválido
}

public sealed record EngineDecision(DecisionOutcome Outcome, EnvelopeVerdict Envelope, string Rationale);

/// <summary>
/// Fecha o loop com guardrails (acatech estágio 6): TODA decisão passa primeiro pelo
/// envelope físico; depois a criticidade decide se executa sozinha ou espera aprovação.
/// Nunca escreve direto no PLC — quem executa é o edge gateway, auditado e reversível.
/// </summary>
public static class ApprovalPolicy
{
    public static EngineDecision Decide(EnvelopeVerdict envelope, Criticality criticality)
    {
        if (envelope != EnvelopeVerdict.WithinEnvelope)
            return new EngineDecision(DecisionOutcome.Rejected, envelope,
                $"Comando fora do envelope de operação: {envelope}.");

        return criticality switch
        {
            Criticality.Low => new EngineDecision(DecisionOutcome.AutoApproved, envelope,
                "Dentro do envelope, criticidade baixa: execução autônoma."),
            _ => new EngineDecision(DecisionOutcome.NeedsHumanApproval, envelope,
                $"Dentro do envelope, criticidade {criticality}: aprovação humana obrigatória."),
        };
    }
}
