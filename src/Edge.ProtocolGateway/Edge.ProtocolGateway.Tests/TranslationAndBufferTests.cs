using Edge.ProtocolGateway.Domain.Buffering;
using Edge.ProtocolGateway.Domain.Translation;
using Xunit;

namespace Edge.ProtocolGateway.Tests;

public class ProtocolTranslatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    // Termopar no registrador 100: valor bruto em décimos de grau, offset -40.
    private static readonly ProtocolTranslator Translator = new([
        new RegisterMapping("temp-forno-01", Address: 100, Scale: 0.1, Offset: -40),
        new RegisterMapping("pressao-linha-02", Address: 101, Scale: 0.01, Offset: 0),
    ]);

    [Fact]
    public void Registrador_mapeado_vira_grandeza_fisica()
    {
        var evt = Translator.TranslateModbus(address: 100, rawValue: 8500, Now);

        Assert.Equal("temp-forno-01", evt.SensorId);
        Assert.Equal(810.0, evt.Value, precision: 6); // 8500 * 0.1 - 40
        Assert.Equal(Now, evt.MeasuredAt);
    }

    [Fact]
    public void Registrador_desconhecido_e_recusado()
    {
        Assert.Throws<UnknownSourceException>(() => Translator.TranslateModbus(999, 1, Now));
    }

    [Theory]
    [InlineData("linha/2/sensor/temp-forno-01", "temp-forno-01")]
    [InlineData("linha/2/sensor/", "")]
    [InlineData("linha/2/outra-coisa/x", null)]
    [InlineData("qualquer/coisa", null)]
    public void Topico_mqtt_padrao_extrai_o_sensor(string topic, string? expected)
    {
        Assert.Equal(expected, ProtocolTranslator.SensorIdFromTopic(topic));
    }
}

public class StoreAndForwardBufferTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
    private static LineEvent Evt(int i) => new($"s-{i}", i, Now);

    [Fact]
    public void Drena_em_ordem_de_chegada()
    {
        var buffer = new StoreAndForwardBuffer(capacity: 10);
        buffer.TryEnqueue(Evt(1));
        buffer.TryEnqueue(Evt(2));
        buffer.TryEnqueue(Evt(3));

        Assert.Equal(["s-1", "s-2"], buffer.DrainBatch(2).Select(e => e.SensorId));
        Assert.Equal(1, buffer.Count);
    }

    [Fact]
    public void Pressao_sobe_com_a_ocupacao()
    {
        var buffer = new StoreAndForwardBuffer(capacity: 10);
        Assert.Equal(BufferPressure.Normal, buffer.Pressure);

        for (var i = 0; i < 8; i++) buffer.TryEnqueue(Evt(i));
        Assert.Equal(BufferPressure.High, buffer.Pressure);

        for (var i = 8; i < 10; i++) buffer.TryEnqueue(Evt(i));
        Assert.Equal(BufferPressure.Saturated, buffer.Pressure);
    }

    [Fact]
    public void Saturado_recusa_em_vez_de_descartar_calado()
    {
        var buffer = new StoreAndForwardBuffer(capacity: 1);
        Assert.True(buffer.TryEnqueue(Evt(1)));
        Assert.False(buffer.TryEnqueue(Evt(2)));
        Assert.Equal(1, buffer.Count); // o que entrou permanece intacto
    }

    [Fact]
    public void Apos_drenar_volta_a_aceitar()
    {
        var buffer = new StoreAndForwardBuffer(capacity: 1);
        buffer.TryEnqueue(Evt(1));
        buffer.DrainBatch(1);
        Assert.True(buffer.TryEnqueue(Evt(2)));
    }
}
