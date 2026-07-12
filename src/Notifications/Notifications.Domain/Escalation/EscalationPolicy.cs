namespace Notifications.Domain.Escalation;

public enum Severity { Info, Warning, Critical }

/// <summary>Um degrau da escada de on-call: quem é acionado e quanto tempo tem pra reconhecer.</summary>
public sealed record EscalationLevel(string Contact, TimeSpan AckTimeout);

public sealed record EscalationDecision(
    string? NotifyNow,          // quem deve ser acionado neste instante (null = ninguém/esgotado)
    int Level,                  // degrau atual (0 = primeiro acionado)
    bool ExhaustedOpenIncident) // escada acabou sem ack → abre incidente formal
{
    public static readonly EscalationDecision Acknowledged = new(null, -1, false);
}

/// <summary>
/// Política de escalonamento: severidade define a escada; o tempo sem reconhecimento
/// define o degrau. Determinística — alerta e relógio entram por parâmetro, a decisão
/// de QUEM acordar às 3h da manhã tem teste unitário.
/// </summary>
public sealed class EscalationPolicy(IReadOnlyDictionary<Severity, IReadOnlyList<EscalationLevel>> ladders)
{
    public EscalationDecision Decide(Severity severity, TimeSpan sinceRaised, bool acknowledged)
    {
        if (acknowledged)
            return EscalationDecision.Acknowledged;

        if (!ladders.TryGetValue(severity, out var ladder) || ladder.Count == 0)
            return new EscalationDecision(null, -1, ExhaustedOpenIncident: severity == Severity.Critical);

        var elapsed = TimeSpan.Zero;
        for (var level = 0; level < ladder.Count; level++)
        {
            elapsed += ladder[level].AckTimeout;
            if (sinceRaised < elapsed)
                return new EscalationDecision(ladder[level].Contact, level, false);
        }

        // Ninguém reconheceu dentro dos prazos: incidente formal.
        return new EscalationDecision(null, ladder.Count, ExhaustedOpenIncident: true);
    }
}
