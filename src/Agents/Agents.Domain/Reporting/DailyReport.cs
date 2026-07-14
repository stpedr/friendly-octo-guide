using Agents.Domain.Diagnosis;

namespace Agents.Domain.Reporting;

public sealed record ResourceLine(string Resource, int Signals, Severity Peak);

/// <summary>Relatório diário da linha — o que o agente agendado gera e publica.</summary>
public sealed record DailyReport(
    DateOnly Day,
    int TotalSignals,
    int Incidents,
    IReadOnlyDictionary<Severity, int> BySeverity,
    IReadOnlyList<ResourceLine> TopResources);

/// <summary>
/// Consolida os sinais do dia num relatório determinístico: totais por severidade e
/// os recursos que mais sofreram. "Incidente" = recurso com ao menos um sinal
/// Error+. Ordena o ranking por severidade de pico e depois volume — o que o gestor
/// olha primeiro fica no topo.
/// </summary>
public static class DailyReportBuilder
{
    public static DailyReport Build(IEnumerable<Signal> signals, DateOnly day, int topN = 5)
    {
        ArgumentNullException.ThrowIfNull(signals);

        var ofDay = signals.Where(s => DateOnly.FromDateTime(s.At.UtcDateTime) == day).ToList();

        var bySeverity = Enum.GetValues<Severity>()
            .ToDictionary(sev => sev, sev => ofDay.Count(s => s.Severity == sev));

        var byResource = ofDay
            .GroupBy(s => s.Resource)
            .Select(g => new ResourceLine(g.Key, g.Count(), g.Max(s => s.Severity)))
            .OrderByDescending(r => r.Peak)
            .ThenByDescending(r => r.Signals)
            .ToList();

        var incidents = byResource.Count(r => r.Peak >= Severity.Error);

        return new DailyReport(day, ofDay.Count, incidents, bySeverity, [.. byResource.Take(topN)]);
    }
}
