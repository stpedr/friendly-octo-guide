namespace Platform.Audit;

/// <summary>
/// Antes de virar evento imutável, todo valor sensível é REDIGIDO — a trilha de
/// auditoria prova QUE uma permissão mudou, nunca expõe o segredo (LGPD). Chave cujo
/// nome bate com um termo sensível tem o valor trocado pelo placeholder; o resto
/// passa. Puro e determinístico — combina com o PiiMasker do ServiceDefaults, mas
/// aqui a regra é por NOME de campo, não por formato do valor.
/// </summary>
public static class AuditRedaction
{
    public const string Placeholder = "[redigido]";

    // Marcadores de nome de campo que jamais podem ter o valor gravado na trilha.
    private static readonly string[] SensitiveKeyMarkers =
    [
        "senha", "password", "hash", "salt", "totp", "seed",
        "secret", "segredo", "token", "apikey", "api_key", "credential",
    ];

    /// <summary>Verdadeiro se o NOME do campo indica um valor sensível.</summary>
    public static bool IsSensitiveKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        foreach (var marker in SensitiveKeyMarkers)
        {
            if (key.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>Redige um mapa chave→valor: chave sensível vira placeholder, resto intacto.</summary>
    public static IReadOnlyDictionary<string, string> Redact(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
            result[key] = IsSensitiveKey(key) ? Placeholder : value;

        return result;
    }
}
