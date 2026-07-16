using Knowledge.Domain.Ishikawa;
using Xunit;

namespace Knowledge.Tests;

public class IshikawaDiagramTests
{
    private static RootCause Cause(IshikawaCategory cat, string causa, double confianca) =>
        new(Guid.NewGuid(), "envase.linha2.enchedora", cat, "SOLDER-BRIDGE", causa, null, confianca,
            DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

    [Fact]
    public void Build_agrupa_por_categoria_e_ordena_por_confianca()
    {
        var causes = new[]
        {
            Cause(IshikawaCategory.Maquina, "estêncil gasto", 0.6),
            Cause(IshikawaCategory.Maquina, "squeegee torto", 0.9),
            Cause(IshikawaCategory.Material, "pasta vencida", 0.5),
        };

        var diagram = IshikawaDiagram.Build(causes);

        Assert.Equal(2, diagram.Count);                       // Máquina e Material
        Assert.Equal(2, diagram[IshikawaCategory.Maquina].Count);
        Assert.Equal("squeegee torto", diagram[IshikawaCategory.Maquina][0].Causa); // maior confiança primeiro
        Assert.False(diagram.ContainsKey(IshikawaCategory.Metodo));                  // categoria sem causa não aparece
    }

    [Fact]
    public void Build_de_lista_vazia_e_vazio()
    {
        Assert.Empty(IshikawaDiagram.Build([]));
    }

    [Fact]
    public void TopCauses_ranqueia_por_confianca_no_geral()
    {
        var causes = new[]
        {
            Cause(IshikawaCategory.Maquina, "a", 0.3),
            Cause(IshikawaCategory.Material, "b", 0.95),
            Cause(IshikawaCategory.Medicao, "c", 0.7),
        };

        var top = IshikawaDiagram.TopCauses(causes, 2);

        Assert.Equal(2, top.Count);
        Assert.Equal("b", top[0].Causa);
        Assert.Equal("c", top[1].Causa);
    }

    [Fact]
    public void TopCauses_com_n_invalido_lanca()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IshikawaDiagram.TopCauses([], 0));
    }
}
