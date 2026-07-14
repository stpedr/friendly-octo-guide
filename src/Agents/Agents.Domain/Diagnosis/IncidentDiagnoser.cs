namespace Agents.Domain.Diagnosis;

/// <summary>
/// Hipótese de causa raiz de um incidente: o recurso afetado, o sinal mais antigo
/// e mais grave do cluster (a origem provável), quantos sinais correlacionaram e
/// uma confiança grosseira. É o que o agente propõe — não a verdade final.
/// </summary>
public sealed record Diagnosis(
    string Resource,
    Signal RootCause,
    int CorrelatedSignals,
    Severity Peak,
    double Confidence,
    IReadOnlyList<string> TraceIds);

/// <summary>
/// Correlaciona sinais dispersos num diagnóstico por recurso: agrupa por
/// `Resource`, mantém só clusters dentro de uma janela de tempo, e para cada um
/// aponta como causa raiz o sinal MAIS ANTIGO entre os de maior severidade — a
/// falha costuma nascer antes do sintoma que dispara o alerta. Determinístico.
/// </summary>
public static class IncidentDiagnoser
{
    /// <summary>Diagnostica o incidente mais grave dentro da janela, ou null se nada relevante.</summary>
    public static Diagnosis? Diagnose(IEnumerable<Signal> signals, TimeSpan window, Severity floor = Severity.Warning)
    {
        ArgumentNullException.ThrowIfNull(signals);

        var relevant = signals.Where(s => s.Severity >= floor).ToList();
        if (relevant.Count == 0)
            return null;

        // O incidente é ancorado no sinal mais grave e mais recente; a janela olha pra trás dele.
        var anchor = relevant
            .OrderByDescending(s => s.Severity)
            .ThenByDescending(s => s.At)
            .First();

        var cluster = relevant
            .Where(s => s.Resource == anchor.Resource && anchor.At - s.At <= window && s.At <= anchor.At)
            .ToList();

        var peak = cluster.Max(s => s.Severity);
        var rootCause = cluster
            .Where(s => s.Severity == peak)
            .OrderBy(s => s.At)
            .First();

        // Confiança cresce com o nº de sinais que concordam e satura em 0.95.
        var confidence = Math.Min(0.95, 0.4 + 0.1 * (cluster.Count - 1));

        return new Diagnosis(
            anchor.Resource,
            rootCause,
            cluster.Count,
            peak,
            confidence,
            [.. cluster.Where(s => s.TraceId is not null).Select(s => s.TraceId!).Distinct()]);
    }
}
