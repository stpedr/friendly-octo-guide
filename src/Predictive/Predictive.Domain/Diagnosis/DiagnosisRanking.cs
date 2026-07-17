using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Predictive.Domain.Diagnosis;

/// <summary>Evidência acumulada de um sintoma num ativo: quantas vezes ocorreu e o peso do modelo. DTO puro.</summary>
[ExcludeFromCodeCoverage]
public sealed record SymptomEvidence(string Sintoma, int Ocorrencias, double PesoModelo = 1.0);

/// <summary>Causa candidata ranqueada, com o porquê (explicabilidade / XAI). DTO puro.</summary>
[ExcludeFromCodeCoverage]
public sealed record RankedCause(string Sintoma, double Score, string Explicacao);

/// <summary>
/// Camada de processamento do iDMSS (proposta Victor): ordena os sintomas candidatos por
/// evidência. Score = ocorrências × peso do modelo — o Random Forest servido fornece o
/// peso; 1.0 é o baseline sem modelo. Determinística e EXPLICÁVEL: cada score carrega a
/// conta que o gerou (XAI), não é caixa-preta. O modelo real pluga trocando o peso; a
/// agregação e o ranking são domínio.
/// </summary>
public static class DiagnosisRanking
{
    public static IReadOnlyList<RankedCause> Rank(IEnumerable<SymptomEvidence> evidencias, int top)
    {
        ArgumentNullException.ThrowIfNull(evidencias);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(top);

        return [.. evidencias
            .Where(e => e.Ocorrencias > 0)
            .Select(e => new RankedCause(
                e.Sintoma,
                e.Ocorrencias * e.PesoModelo,
                string.Create(CultureInfo.InvariantCulture, $"{e.Ocorrencias}× · peso {e.PesoModelo:0.00}")))
            .OrderByDescending(c => c.Score)
            .Take(top)];
    }
}
