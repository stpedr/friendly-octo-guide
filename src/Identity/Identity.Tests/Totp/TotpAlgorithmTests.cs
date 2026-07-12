using Identity.Domain.Totp;
using Xunit;

namespace Identity.Tests.Totp;

public class TotpAlgorithmTests
{
    // Seed dos vetores oficiais do RFC 6238 (Apêndice B): ASCII "12345678901234567890".
    private static readonly byte[] RfcSeed = System.Text.Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    // (unix time, código esperado) — vetores do RFC truncados a 6 dígitos.
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void Gera_os_vetores_de_teste_do_RFC_6238(long unixTime, string expected)
    {
        var instant = DateTimeOffset.FromUnixTimeSeconds(unixTime);
        Assert.Equal(expected, TotpAlgorithm.CodeAt(RfcSeed, instant));
    }

    [Fact]
    public void Mesma_janela_de_30s_gera_o_mesmo_codigo()
    {
        var a = TotpAlgorithm.CodeAt(RfcSeed, DateTimeOffset.FromUnixTimeSeconds(30));
        var b = TotpAlgorithm.CodeAt(RfcSeed, DateTimeOffset.FromUnixTimeSeconds(59));
        Assert.Equal(a, b);
    }

    [Fact]
    public void Janelas_distintas_geram_codigos_distintos()
    {
        var a = TotpAlgorithm.CodeAt(RfcSeed, DateTimeOffset.FromUnixTimeSeconds(59));
        var b = TotpAlgorithm.CodeAt(RfcSeed, DateTimeOffset.FromUnixTimeSeconds(60));
        Assert.NotEqual(a, b);
    }
}

public class Base32Tests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Codifica_os_vetores_do_RFC_4648(string plain, string encoded)
    {
        Assert.Equal(encoded, Base32.Encode(System.Text.Encoding.ASCII.GetBytes(plain)));
    }

    [Fact]
    public void Roundtrip_de_seed_aleatoria_preserva_os_bytes()
    {
        var seed = new byte[20];
        Random.Shared.NextBytes(seed);
        Assert.Equal(seed, Base32.Decode(Base32.Encode(seed)));
    }

    [Fact]
    public void Caractere_fora_do_alfabeto_lanca_FormatException()
    {
        Assert.Throws<FormatException>(() => Base32.Decode("ABC1")); // '1' não existe em Base32
    }
}
