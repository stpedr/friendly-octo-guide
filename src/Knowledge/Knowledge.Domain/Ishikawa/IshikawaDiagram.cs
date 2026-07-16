namespace Knowledge.Domain.Ishikawa;

/// <summary>
/// Monta a espinha de peixe: agrupa causas por categoria 6M e ranqueia por confiança.
/// É a estrutura que o iDMSS mostra e sobre a qual a inferência opera. Pura.
/// </summary>
public static class IshikawaDiagram
{
    /// <summary>
    /// Agrupa por categoria; cada grupo ordenado por confiança (desc). Categoria sem
    /// causa não aparece.
    /// </summary>
    public static IReadOnlyDictionary<IshikawaCategory, IReadOnlyList<RootCause>> Build(
        IEnumerable<RootCause> causes)
    {
        ArgumentNullException.ThrowIfNull(causes);

        return causes
            .GroupBy(c => c.Categoria)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<RootCause>)g.OrderByDescending(c => c.Confianca).ToList());
    }

    /// <summary>As <paramref name="n"/> causas de maior confiança no geral — o ranking pro iDMSS.</summary>
    public static IReadOnlyList<RootCause> TopCauses(IEnumerable<RootCause> causes, int n)
    {
        ArgumentNullException.ThrowIfNull(causes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(n);

        return [.. causes.OrderByDescending(c => c.Confianca).Take(n)];
    }
}
