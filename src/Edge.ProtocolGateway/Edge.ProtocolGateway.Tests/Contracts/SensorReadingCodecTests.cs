using Platform.Contracts;
using Xunit;

namespace Edge.ProtocolGateway.Tests.Contracts;

/// <summary>
/// O codec é o contrato executável de schemas/sensor-reading.avsc —
/// roundtrip e casos de borda garantem que edge e ingest falam o mesmo binário.
/// </summary>
public class SensorReadingCodecTests
{
    [Fact]
    public void Roundtrip_preserva_todos_os_campos()
    {
        var original = new SensorReadingRecord(
            "temp-forno-01", 812.5,
            DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_123), QualityFlag: 2);

        var decoded = SensorReadingCodec.Decode(SensorReadingCodec.Encode(original));

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Valores_negativos_e_zero_sobrevivem_ao_zigzag()
    {
        var original = new SensorReadingRecord("s", -273.15, DateTimeOffset.UnixEpoch, 0);
        Assert.Equal(original, SensorReadingCodec.Decode(SensorReadingCodec.Encode(original)));
    }

    [Fact]
    public void Sensor_id_com_utf8_multibyte_sobrevive()
    {
        var original = new SensorReadingRecord("sensor-ção-µ", 1.0, DateTimeOffset.UnixEpoch, 0);
        Assert.Equal(original, SensorReadingCodec.Decode(SensorReadingCodec.Encode(original)));
    }

    [Fact]
    public void Payload_truncado_lanca_FormatException_e_nao_le_lixo()
    {
        var bytes = SensorReadingCodec.Encode(new SensorReadingRecord("temp-forno-01", 812.5, DateTimeOffset.UnixEpoch));
        Assert.Throws<FormatException>(() => SensorReadingCodec.Decode(bytes.AsSpan(0, bytes.Length - 3)));
    }

    [Fact]
    public void Payload_vazio_lanca_FormatException()
    {
        Assert.Throws<FormatException>(() => SensorReadingCodec.Decode([]));
    }
}
