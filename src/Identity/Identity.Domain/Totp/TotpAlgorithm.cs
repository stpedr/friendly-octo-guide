using System.Security.Cryptography;

namespace Identity.Domain.Totp;

/// <summary>
/// TOTP (RFC 6238) sobre HOTP (RFC 4226): HMAC-SHA1 do contador de janelas de 30s,
/// truncamento dinâmico, 6 dígitos. Determinístico — o relógio entra por parâmetro,
/// validado contra os vetores de teste do próprio RFC.
/// </summary>
public static class TotpAlgorithm
{
    public const int Digits = 6;
    public static readonly TimeSpan Step = TimeSpan.FromSeconds(30);

    public static long StepOf(DateTimeOffset instant) => instant.ToUnixTimeSeconds() / (long)Step.TotalSeconds;

    public static string CodeAt(byte[] seed, DateTimeOffset instant) => CodeAtStep(seed, StepOf(instant));

    // CA5350: HMAC-SHA1 é o algoritmo QUE O RFC 6238 exige pra interoperar com
    // Google Authenticator etc. Não protege dado em repouso — gera um código de 30s.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5350")]
    public static string CodeAtStep(byte[] seed, long step)
    {
        ArgumentNullException.ThrowIfNull(seed);

        Span<byte> counter = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counter, step);

        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(seed, counter, hash);

        // Truncamento dinâmico (RFC 4226 §5.3): offset nos 4 bits finais do hash.
        var offset = hash[^1] & 0x0F;
        var binCode = ((hash[offset] & 0x7F) << 24)
                    | (hash[offset + 1] << 16)
                    | (hash[offset + 2] << 8)
                    | hash[offset + 3];

        return (binCode % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }
}
