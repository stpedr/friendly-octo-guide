namespace Platform.ServiceDefaults.Security;

/// <summary>
/// Mascaramento de PII pra log e telemetria: o dado sensível NUNCA aparece em
/// claro num log, nem por acidente. Mascarar não é cifrar — o objetivo é
/// observabilidade sem vazamento (LGPD), preservando o suficiente pra depurar
/// (domínio do e-mail, últimos dígitos de um documento). Determinístico e puro.
/// </summary>
public static class PiiMasker
{
    private const string Empty = "***";

    /// <summary>Genérico: mantém o primeiro caractere, mascara o resto.</summary>
    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return Empty;
        return value.Length == 1 ? "*" : value[0] + new string('*', value.Length - 1);
    }

    /// <summary>E-mail: mascara o local, mantém o domínio (útil pra suporte sem expor a pessoa).</summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrEmpty(email))
            return Empty;
        var at = email.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0)
            return Mask(email); // não é e-mail — trata como genérico
        var local = email[..at];
        var masked = local.Length == 1 ? "*" : local[0] + new string('*', local.Length - 1);
        return masked + email[at..];
    }

    /// <summary>Documento/telefone: mantém os últimos <paramref name="keep"/> dígitos, mascara o resto.</summary>
    public static string Tail(string? value, int keep = 4)
    {
        if (string.IsNullOrEmpty(value))
            return Empty;
        if (value.Length <= keep)
            return new string('*', value.Length);
        return new string('*', value.Length - keep) + value[^keep..];
    }
}
