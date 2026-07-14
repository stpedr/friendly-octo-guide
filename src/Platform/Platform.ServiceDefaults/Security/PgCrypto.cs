namespace Platform.ServiceDefaults.Security;

/// <summary>
/// Criptografia em repouso de coluna com pgcrypto (pgp_sym_*): campo sensível é
/// cifrado no banco com uma chave que vem do OpenBao, não fica em claro no disco
/// nem num dump. Helper de fragmentos SQL parametrizados — o chamador passa os
/// parâmetros ($1, $2…), a chave nunca entra concatenada na query.
/// </summary>
public static class PgCrypto
{
    /// <summary>Rode uma vez por banco (idempotente) antes de usar coluna cifrada.</summary>
    public const string EnableExtension = "CREATE EXTENSION IF NOT EXISTS pgcrypto;";

    /// <summary>Expressão de escrita: cifra <paramref name="valueParam"/> com <paramref name="keyParam"/>.</summary>
    public static string EncryptExpr(string valueParam, string keyParam)
    {
        Validate(valueParam);
        Validate(keyParam);
        return $"pgp_sym_encrypt({valueParam}, {keyParam})";
    }

    /// <summary>Expressão de leitura: decifra a coluna/expressão cifrada.</summary>
    public static string DecryptExpr(string encryptedColumn, string keyParam)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedColumn);
        Validate(keyParam);
        return $"pgp_sym_decrypt({encryptedColumn}, {keyParam})";
    }

    // Parâmetro tem que ser posicional ($n) ou nomeado (@x) — nunca literal, senão
    // vira porta de injeção e a chave em claro na query.
    private static void Validate(string param)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(param);
        if (param[0] is not ('$' or '@'))
            throw new ArgumentException($"Parâmetro '{param}' deve ser placeholder ($n/@x), não literal.", nameof(param));
    }
}
