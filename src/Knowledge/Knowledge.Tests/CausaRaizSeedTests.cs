using Knowledge.Domain.Ishikawa;
using Xunit;

namespace Knowledge.Tests;

public class CausaRaizSeedTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
    private static Guid NewId() => Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Semeia_causa_classificada_por_sintoma()
    {
        var causas = CausaRaizSeed.FromSintomas(
            [("desgaste de estêncil", "ENCH-JAM-02"), ("umidade alta", null)],
            "envase.linha2.enchedora", Now, NewId);

        Assert.Equal(2, causas.Count);
        Assert.Equal(IshikawaCategory.Maquina, causas[0].Categoria);
        Assert.Equal("ENCH-JAM-02", causas[0].MotivoCodigo);
        Assert.Equal(CausaRaizSeed.ConfiancaInicial, causas[0].Confianca);
        Assert.Equal(IshikawaCategory.MeioAmbiente, causas[1].Categoria);
        Assert.Null(causas[1].MotivoCodigo);
    }

    [Fact]
    public void Sintoma_em_branco_e_ignorado()
    {
        var causas = CausaRaizSeed.FromSintomas(
            [("  ", "X"), ("solda insuficiente", null)],
            "ativo", Now, NewId);

        Assert.Single(causas);
        Assert.Equal(IshikawaCategory.Material, causas[0].Categoria);
    }

    [Fact]
    public void Lista_vazia_gera_nada()
    {
        Assert.Empty(CausaRaizSeed.FromSintomas([], "ativo", Now, NewId));
    }

    [Fact]
    public void Ativo_em_branco_lanca()
    {
        Assert.Throws<ArgumentException>(() =>
            CausaRaizSeed.FromSintomas([("x", null)], "", Now, NewId));
    }
}
