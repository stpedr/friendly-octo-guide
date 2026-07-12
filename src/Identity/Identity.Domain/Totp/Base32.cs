namespace Identity.Domain.Totp;

/// <summary>
/// Base32 (RFC 4648) — o formato que apps authenticator esperam no QR de provisionamento.
/// Só o alfabeto padrão, sem padding opcional exótico: seed é gerada por nós, não importada.
/// </summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        var output = new System.Text.StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                output.Append(Alphabet[(buffer >> bits) & 0x1F]);
            }
        }

        if (bits > 0)
            output.Append(Alphabet[(buffer << (5 - bits)) & 0x1F]);

        return output.ToString();
    }

    public static byte[] Decode(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);

        var output = new List<byte>(encoded.Length * 5 / 8);
        int buffer = 0, bits = 0;

        foreach (var c in encoded.TrimEnd('=').ToUpperInvariant())
        {
            var index = Alphabet.IndexOf(c, StringComparison.Ordinal);
            if (index < 0)
                throw new FormatException($"Caractere inválido em Base32: '{c}'");

            buffer = (buffer << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)((buffer >> bits) & 0xFF));
            }
        }

        return [.. output];
    }
}
