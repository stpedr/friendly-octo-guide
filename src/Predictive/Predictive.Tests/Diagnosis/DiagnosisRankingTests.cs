using Predictive.Domain.Diagnosis;
using Xunit;

namespace Predictive.Tests.Diagnosis;

public class DiagnosisRankingTests
{
    [Fact]
    public void Ranqueia_por_score_ocorrencias_vezes_peso()
    {
        var ranked = DiagnosisRanking.Rank(
            [
                new SymptomEvidence("estêncil gasto", Ocorrencias: 2, PesoModelo: 0.9),   // 1.8
                new SymptomEvidence("pasta vencida", Ocorrencias: 5, PesoModelo: 0.2),     // 1.0
                new SymptomEvidence("operador novo", Ocorrencias: 1, PesoModelo: 1.5),     // 1.5
            ],
            top: 3);

        Assert.Equal(3, ranked.Count);
        Assert.Equal("estêncil gasto", ranked[0].Sintoma);   // 1.8
        Assert.Equal("operador novo", ranked[1].Sintoma);    // 1.5
        Assert.Equal("pasta vencida", ranked[2].Sintoma);    // 1.0
    }

    [Fact]
    public void Explicacao_carrega_a_conta_xai()
    {
        var ranked = DiagnosisRanking.Rank([new SymptomEvidence("x", 3, 0.5)], top: 1);

        Assert.Contains("3×", ranked[0].Explicacao, StringComparison.Ordinal);
        Assert.Contains("0.50", ranked[0].Explicacao, StringComparison.Ordinal);
    }

    [Fact]
    public void Evidencia_sem_ocorrencia_e_ignorada()
    {
        var ranked = DiagnosisRanking.Rank(
            [new SymptomEvidence("nunca visto", 0), new SymptomEvidence("visto", 1)],
            top: 5);

        Assert.Single(ranked);
        Assert.Equal("visto", ranked[0].Sintoma);
    }

    [Fact]
    public void Top_limita_o_resultado()
    {
        var ranked = DiagnosisRanking.Rank(
            [new SymptomEvidence("a", 1), new SymptomEvidence("b", 2), new SymptomEvidence("c", 3)],
            top: 2);

        Assert.Equal(2, ranked.Count);
        Assert.Equal("c", ranked[0].Sintoma);
    }

    [Fact]
    public void Top_invalido_lanca()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DiagnosisRanking.Rank([], 0));
    }
}
