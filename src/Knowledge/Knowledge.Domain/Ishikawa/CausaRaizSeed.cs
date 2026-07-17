namespace Knowledge.Domain.Ishikawa;

/// <summary>
/// Semeia a base de causa raiz a partir de sintomas conhecidos (motivos de parada do
/// OEE, códigos de defeito): cada sintoma vira uma hipótese de causa, classificada na
/// categoria 6M pelo <see cref="IshikawaClassifier"/>, com confiança inicial baixa (é
/// hipótese a validar, não verdade). Pura — id e instante entram por função.
/// </summary>
public static class CausaRaizSeed
{
    public const double ConfiancaInicial = 0.3;

    public static IReadOnlyList<RootCause> FromSintomas(
        IEnumerable<(string Sintoma, string? MotivoCodigo)> sintomas,
        string ativoId,
        DateTimeOffset agora,
        Func<Guid> newId)
    {
        ArgumentNullException.ThrowIfNull(sintomas);
        ArgumentException.ThrowIfNullOrWhiteSpace(ativoId);
        ArgumentNullException.ThrowIfNull(newId);

        var causas = new List<RootCause>();
        foreach (var (sintoma, motivo) in sintomas)
        {
            if (string.IsNullOrWhiteSpace(sintoma))
                continue;

            var categoria = IshikawaClassifier.Classify(sintoma);
            causas.Add(new RootCause(
                newId(), ativoId, categoria, sintoma.Trim(),
                Causa: $"(hipótese) categoria {categoria} — a investigar",
                MotivoCodigo: motivo,
                Confianca: ConfiancaInicial,
                RegistradoEm: agora));
        }

        return causas;
    }
}
