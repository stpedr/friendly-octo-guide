using Agents.Domain.Diagnosis;
using Xunit;

namespace Agents.Tests;

public class IncidentDiagnoserTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 13, 14, 0, 0, TimeSpan.Zero);
    private static Signal S(string resource, Severity sev, int minute, string? trace = null) =>
        new(SignalKind.Alert, resource, sev, T0.AddMinutes(minute), $"{resource}@{minute}", trace);

    [Fact]
    public void Sem_sinal_relevante_nao_ha_diagnostico()
    {
        var d = IncidentDiagnoser.Diagnose([S("linha-2", Severity.Info, 0)], TimeSpan.FromMinutes(30));
        Assert.Null(d);
    }

    [Fact]
    public void Causa_raiz_e_o_sinal_mais_antigo_entre_os_de_maior_severidade()
    {
        // A prensa esquenta (14:05, Error) antes de o alarme crítico disparar (14:20).
        var signals = new[]
        {
            S("prensa-3", Severity.Warning, 0),
            S("prensa-3", Severity.Error, 5, "trace-a"),
            S("prensa-3", Severity.Critical, 18),
            S("prensa-3", Severity.Critical, 20, "trace-b"),
        };

        var d = IncidentDiagnoser.Diagnose(signals, TimeSpan.FromMinutes(30));

        Assert.NotNull(d);
        Assert.Equal("prensa-3", d!.Resource);
        Assert.Equal(Severity.Critical, d.Peak);
        Assert.Equal(18, (d.RootCause.At - T0).TotalMinutes); // o crítico mais ANTIGO, não o que disparou
        Assert.Equal(4, d.CorrelatedSignals);
    }

    [Fact]
    public void So_correlaciona_o_mesmo_recurso()
    {
        var signals = new[]
        {
            S("linha-2", Severity.Critical, 20),
            S("linha-9", Severity.Critical, 19), // outro recurso não entra no cluster
        };

        var d = IncidentDiagnoser.Diagnose(signals, TimeSpan.FromMinutes(30));
        Assert.Equal("linha-2", d!.Resource);
        Assert.Equal(1, d.CorrelatedSignals);
    }

    [Fact]
    public void Sinais_fora_da_janela_nao_entram()
    {
        var signals = new[]
        {
            S("linha-2", Severity.Error, 0),   // 40 min antes do âncora
            S("linha-2", Severity.Critical, 40),
        };

        var d = IncidentDiagnoser.Diagnose(signals, TimeSpan.FromMinutes(30));
        Assert.Equal(1, d!.CorrelatedSignals);
    }

    [Fact]
    public void Traceids_do_cluster_sao_coletados_para_o_pivo_de_investigacao()
    {
        var signals = new[]
        {
            S("linha-2", Severity.Error, 5, "trace-a"),
            S("linha-2", Severity.Critical, 20, "trace-b"),
            S("linha-2", Severity.Critical, 21, "trace-b"), // dedup
        };

        var d = IncidentDiagnoser.Diagnose(signals, TimeSpan.FromMinutes(30));
        Assert.Equal(["trace-b", "trace-a"], d!.TraceIds.OrderByDescending(x => x));
    }
}
