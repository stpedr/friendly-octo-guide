using Agents.Api;
using Agents.Domain.Actions;
using Agents.Domain.Diagnosis;
using Agents.Domain.Reporting;
using Xunit;

namespace Agents.Tests;

public class DailyReportAndActionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
    private static Signal S(string resource, Severity sev, int hour) =>
        new(SignalKind.Alert, resource, sev, T0.AddHours(hour), "x");

    [Fact]
    public void Relatorio_conta_incidentes_e_ranqueia_por_severidade()
    {
        var day = DateOnly.FromDateTime(T0.UtcDateTime);
        var signals = new[]
        {
            S("linha-2", Severity.Critical, 0),
            S("linha-2", Severity.Warning, 1),
            S("linha-5", Severity.Warning, 2),
            S("linha-8", Severity.Error, 3),
        };

        var report = DailyReportBuilder.Build(signals, day);

        Assert.Equal(4, report.TotalSignals);
        Assert.Equal(2, report.Incidents); // linha-2 (Critical) e linha-8 (Error)
        Assert.Equal("linha-2", report.TopResources[0].Resource); // pico mais alto no topo
        Assert.Equal(1, report.BySeverity[Severity.Error]);
    }

    [Fact]
    public void Relatorio_ignora_sinais_de_outro_dia()
    {
        var day = DateOnly.FromDateTime(T0.UtcDateTime);
        var signals = new[] { S("linha-2", Severity.Error, 0), S("linha-2", Severity.Critical, 24) };
        var report = DailyReportBuilder.Build(signals, day);
        Assert.Equal(1, report.TotalSignals);
    }

    [Theory]
    [InlineData(ActionKind.Read, false, ActionVerdict.Proceed)]
    [InlineData(ActionKind.OpenTicket, false, ActionVerdict.Proceed)]
    [InlineData(ActionKind.Corrective, false, ActionVerdict.NeedsHumanConfirmation)]
    [InlineData(ActionKind.Corrective, true, ActionVerdict.Proceed)]
    [InlineData(ActionKind.Physical, true, ActionVerdict.RoutedToDecisionEngine)]
    public void Guardrail_decide_por_tipo_de_acao(ActionKind kind, bool confirmed, ActionVerdict expected)
        => Assert.Equal(expected, AgentActionPolicy.Evaluate(kind, confirmed));

    [Fact]
    public void Acao_fisica_nunca_sai_direto_do_agente_mesmo_confirmada()
        => Assert.Equal(ActionVerdict.RoutedToDecisionEngine, AgentActionPolicy.Evaluate(ActionKind.Physical, true));

    [Fact]
    public void Janela_poda_sinais_alem_da_retencao()
    {
        var window = new SignalWindow(TimeSpan.FromHours(1));
        window.Add(S("linha-2", Severity.Error, 0), T0);                 // sinal às 10:00
        window.Add(S("linha-2", Severity.Critical, 2), T0.AddHours(2));  // às 12:00 → poda o das 10:00
        Assert.Single(window.Snapshot());
    }

    [Fact]
    public void Delay_ate_a_proxima_hora_alvo_pula_pro_dia_seguinte_se_ja_passou()
    {
        var now = new DateTimeOffset(2026, 7, 13, 8, 0, 0, TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromHours(22), DailyReportService.DelayUntilNext(now, hourUtc: 6));  // amanhã 06:00
        Assert.Equal(TimeSpan.FromHours(1), DailyReportService.DelayUntilNext(now, hourUtc: 9));   // hoje 09:00
    }
}
