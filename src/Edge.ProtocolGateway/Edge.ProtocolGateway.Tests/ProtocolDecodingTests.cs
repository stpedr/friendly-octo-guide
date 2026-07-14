using Edge.ProtocolGateway.Domain.Translation;
using Xunit;

namespace Edge.ProtocolGateway.Tests;

public class ProtocolDecodingTests
{
    private static readonly DateTimeOffset T = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Int16_com_sinal_decodifica_negativo()
    {
        var t = new ProtocolTranslator([new RegisterMapping("temp", 10, Scale: 0.1, Offset: 0, RegisterType.Signed16)]);
        // 0xFFEC = -20 → *0.1 = -2.0 °C
        var e = t.TranslateModbus(10, [0xFFEC], T);
        Assert.Equal(-2.0, e.Value, precision: 6);
    }

    [Fact]
    public void Int32_junta_dois_registradores_big_endian()
    {
        var t = new ProtocolTranslator([new RegisterMapping("contador", 20, 1, 0, RegisterType.Signed32)]);
        // 0x0001_86A0 = 100000
        var e = t.TranslateModbus(20, [0x0001, 0x86A0], T);
        Assert.Equal(100000, e.Value, precision: 6);
    }

    [Fact]
    public void Float32_decodifica_ieee754()
    {
        var t = new ProtocolTranslator([new RegisterMapping("pressao", 30, 1, 0, RegisterType.Real32)]);
        // 12.5f = 0x4148_0000
        var e = t.TranslateModbus(30, [0x4148, 0x0000], T);
        Assert.Equal(12.5, e.Value, precision: 4);
    }

    [Fact]
    public void Quantidade_de_words_errada_e_recusada()
    {
        var t = new ProtocolTranslator([new RegisterMapping("contador", 20, 1, 0, RegisterType.Signed32)]);
        Assert.Throws<UnknownSourceException>(() => t.TranslateModbus(20, [0x0001], T)); // 32 bits exige 2 words
    }

    [Fact]
    public void Opcua_mapeia_no_para_sensor_com_escala()
    {
        var t = new ProtocolTranslator(
            registerMap: [],
            opcUaMap: [new OpcUaMapping("prensa-3-temp", "ns=2;s=Prensa3.Temp", Scale: 1.0, Offset: -273.15)]);

        var e = t.TranslateOpcUa("ns=2;s=Prensa3.Temp", 300.15, T); // Kelvin → Celsius
        Assert.Equal("prensa-3-temp", e.SensorId);
        Assert.Equal(27.0, e.Value, precision: 4);
    }

    [Fact]
    public void No_opcua_desconhecido_e_recusado()
    {
        var t = new ProtocolTranslator([]);
        Assert.Throws<UnknownSourceException>(() => t.TranslateOpcUa("ns=9;s=Fantasma", 1.0, T));
    }
}
