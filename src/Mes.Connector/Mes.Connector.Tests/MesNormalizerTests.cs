using Mes.Connector.Domain;
using Xunit;

namespace Mes.Connector.Tests;

public class MesNormalizerTests
{
    private static readonly Guid FixedId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static Guid NewId() => FixedId;

    private static RawMesRow Row(
        string ativo = "envase.linha2.enchedora",
        string tipo = "Defeito",
        string codigo = "SOLDER-BRIDGE",
        string? quantidade = "1",
        string? occurredAt = null) =>
        new("000000000000000001", ativo, tipo, codigo, quantidade, Texto: " obs ", Turno: " dia ", OccurredAt: occurredAt);

    [Fact]
    public void Linha_valida_vira_evento_com_campos_convertidos()
    {
        var result = MesNormalizer.Normalize(Row(occurredAt: "2026-07-16T10:00:00Z"), "simulador", NewId);

        Assert.True(result.Accepted);
        var evt = result.Event!;
        Assert.Equal(FixedId, evt.EventId);
        Assert.Equal("envase.linha2.enchedora", evt.AtivoId);
        Assert.Equal(MesEventType.Defeito, evt.Tipo);
        Assert.Equal(1.0, evt.Quantidade!.Value);
        Assert.Equal("obs", evt.Texto);       // trim aplicado
        Assert.Equal("dia", evt.Turno);        // trim aplicado
        Assert.Equal("simulador", evt.SistemaOrigem);
        Assert.Equal(DateTimeOffset.Parse("2026-07-16T10:00:00Z"), evt.OccurredAt);
    }

    [Fact]
    public void Sem_occurred_at_usa_agora_e_aceita()
    {
        var result = MesNormalizer.Normalize(Row(occurredAt: null), "simulador", NewId);
        Assert.True(result.Accepted);
    }

    [Theory]
    [InlineData("", "Defeito", "COD", "AtivoAusente")]
    [InlineData("ativo", "Defeito", "", "CodigoAusente")]
    [InlineData("ativo", "Explodiu", "COD", "TipoInvalido")]
    [InlineData("ativo", "99", "COD", "TipoInvalido")]
    public void Campo_obrigatorio_ou_tipo_invalido_e_rejeitado(string ativo, string tipo, string codigo, string motivo)
    {
        var result = MesNormalizer.Normalize(
            new RawMesRow("c1", ativo, tipo, codigo), "simulador", NewId);

        Assert.False(result.Accepted);
        Assert.Null(result.Event);
        Assert.Equal(motivo, result.Reason);
    }

    [Fact]
    public void Quantidade_nao_numerica_e_rejeitada()
    {
        var result = MesNormalizer.Normalize(Row(quantidade: "abc"), "simulador", NewId);
        Assert.False(result.Accepted);
        Assert.Equal("QuantidadeInvalida", result.Reason);
    }

    [Fact]
    public void Timestamp_malformado_e_rejeitado()
    {
        var result = MesNormalizer.Normalize(Row(occurredAt: "ontem"), "simulador", NewId);
        Assert.False(result.Accepted);
        Assert.Equal("TimestampInvalido", result.Reason);
    }

    [Fact]
    public void Quantidade_vazia_vira_null_sem_rejeitar()
    {
        var result = MesNormalizer.Normalize(Row(quantidade: null), "simulador", NewId);
        Assert.True(result.Accepted);
        Assert.Null(result.Event!.Quantidade);
    }
}
