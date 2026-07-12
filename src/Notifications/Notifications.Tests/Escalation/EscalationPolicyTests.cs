using Notifications.Domain.Escalation;
using Xunit;

namespace Notifications.Tests.Escalation;

public class EscalationPolicyTests
{
    // Escada de crítico: on-call primário (5 min) → secundário (10 min) → gestor (15 min).
    private static readonly EscalationPolicy Policy = new(
        new Dictionary<Severity, IReadOnlyList<EscalationLevel>>
        {
            [Severity.Critical] =
            [
                new("oncall-primario", TimeSpan.FromMinutes(5)),
                new("oncall-secundario", TimeSpan.FromMinutes(10)),
                new("gestor", TimeSpan.FromMinutes(15)),
            ],
            [Severity.Warning] = [new("oncall-primario", TimeSpan.FromHours(1))],
        });

    [Fact]
    public void Alerta_novo_aciona_o_primeiro_degrau()
    {
        var d = Policy.Decide(Severity.Critical, TimeSpan.FromMinutes(1), acknowledged: false);
        Assert.Equal("oncall-primario", d.NotifyNow);
        Assert.Equal(0, d.Level);
    }

    [Fact]
    public void Sem_ack_no_prazo_escala_pro_proximo_degrau()
    {
        var d = Policy.Decide(Severity.Critical, TimeSpan.FromMinutes(7), acknowledged: false);
        Assert.Equal("oncall-secundario", d.NotifyNow);
        Assert.Equal(1, d.Level);
    }

    [Fact]
    public void Escada_esgotada_abre_incidente_formal()
    {
        var d = Policy.Decide(Severity.Critical, TimeSpan.FromMinutes(31), acknowledged: false);
        Assert.Null(d.NotifyNow);
        Assert.True(d.ExhaustedOpenIncident);
    }

    [Fact]
    public void Ack_encerra_o_escalonamento()
    {
        var d = Policy.Decide(Severity.Critical, TimeSpan.FromMinutes(20), acknowledged: true);
        Assert.Null(d.NotifyNow);
        Assert.False(d.ExhaustedOpenIncident);
    }

    [Fact]
    public void Severidade_sem_escada_configurada_nao_aciona_ninguem()
    {
        var d = Policy.Decide(Severity.Info, TimeSpan.FromMinutes(1), acknowledged: false);
        Assert.Null(d.NotifyNow);
        Assert.False(d.ExhaustedOpenIncident);
    }
}

public class ChannelRouterTests
{
    [Fact]
    public void Critico_vai_de_push_e_email()
    {
        Assert.Equal([Channel.Push, Channel.Email], ChannelRouter.ChannelsFor(Severity.Critical));
    }

    [Fact]
    public void Warning_vai_so_de_push()
    {
        Assert.Equal([Channel.Push], ChannelRouter.ChannelsFor(Severity.Warning));
    }

    [Fact]
    public void Info_vai_so_de_email()
    {
        Assert.Equal([Channel.Email], ChannelRouter.ChannelsFor(Severity.Info));
    }
}
