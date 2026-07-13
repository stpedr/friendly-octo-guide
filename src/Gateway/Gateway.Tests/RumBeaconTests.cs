using System.Text;
using Gateway.Domain;
using Xunit;

namespace Gateway.Tests;

public class RumBeaconTests
{
    private static bool Parse(string json, out RumBeacon? beacon) =>
        RumBeacon.TryParse(Encoding.UTF8.GetBytes(json), out beacon);

    [Fact]
    public void Beacon_valido_do_pwa_e_aceito()
    {
        var ok = Parse("""{"route":"/v1/core/ordens","status":200,"durationMs":42.5,"ts":1700000000000}""", out var beacon);

        Assert.True(ok);
        Assert.Equal("/v1/core/ordens", beacon!.Route);
        Assert.Equal(200, beacon.Status);
        Assert.Equal(42.5, beacon.DurationMs);
    }

    [Theory]
    [InlineData("""{"status":200,"durationMs":1}""")]                          // sem rota
    [InlineData("""{"route":"sem-barra","status":200,"durationMs":1}""")]      // rota não começa com /
    [InlineData("""{"route":"/x","status":99,"durationMs":1}""")]              // status implausível
    [InlineData("""{"route":"/x","status":600,"durationMs":1}""")]
    [InlineData("""{"route":"/x","status":200,"durationMs":-1}""")]            // duração negativa
    [InlineData("""[1,2,3]""")]                                                // não é objeto
    [InlineData("""nem json""")]
    [InlineData("")]
    public void Beacon_malformado_e_recusado(string json)
    {
        Assert.False(Parse(json, out _));
    }

    [Fact]
    public void Payload_acima_do_teto_e_recusado_sem_parsear()
    {
        var oversized = "{\"route\":\"/" + new string('a', RumBeacon.MaxPayloadBytes) + "\"}";
        Assert.False(Parse(oversized, out _));
    }

    [Fact]
    public void Rota_gigante_e_recusada_para_conter_cardinalidade_de_metrica()
    {
        var json = $$"""{"route":"/{{new string('r', 200)}}","status":200,"durationMs":1}""";
        Assert.False(Parse(json, out _));
    }
}
