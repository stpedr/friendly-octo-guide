using System.Buffers.Binary;

namespace Platform.Contracts;

/// <summary>Espelho de schemas/sensor-reading.avsc — linha.telemetria.v1.</summary>
public sealed record SensorReadingRecord(
    string SensorId,
    double Value,
    DateTimeOffset MeasuredAt,
    int QualityFlag = 0,
    int ClockSource = 0); // 0=Unknown,1=Ntp,2=Ptp,3=Unsynced — ver schemas/sensor-reading.avsc

/// <summary>
/// Codec Avro binário (spec 1.11) do registro SensorReading, escrito à mão porque o
/// schema é pequeno e estável: string = varint(len)+utf8, double = 8 bytes LE,
/// long/int = zigzag varint. Sem Schema Registry na fase 0 — o schema é fixado em
/// schemas/ e este codec é o contrato executável; o registry entra na fase 1 sem
/// mudar um byte do formato.
/// </summary>
public static class SensorReadingCodec
{
    public static byte[] Encode(SensorReadingRecord reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var utf8 = System.Text.Encoding.UTF8.GetBytes(reading.SensorId);
        var buffer = new byte[10 + utf8.Length + 8 + 10 + 5 + 5];
        var pos = 0;

        pos += WriteZigZag(buffer.AsSpan(pos), utf8.Length);
        utf8.CopyTo(buffer.AsSpan(pos));
        pos += utf8.Length;

        BinaryPrimitives.WriteDoubleLittleEndian(buffer.AsSpan(pos), reading.Value);
        pos += 8;

        pos += WriteZigZag(buffer.AsSpan(pos), reading.MeasuredAt.ToUnixTimeMilliseconds());
        pos += WriteZigZag(buffer.AsSpan(pos), reading.QualityFlag);
        pos += WriteZigZag(buffer.AsSpan(pos), reading.ClockSource);

        return buffer[..pos];
    }

    public static SensorReadingRecord Decode(ReadOnlySpan<byte> payload)
    {
        var pos = 0;

        var len = checked((int)ReadZigZag(payload, ref pos));
        if (len < 0 || pos + len > payload.Length)
            throw new FormatException("Payload Avro truncado: comprimento de sensor_id inválido.");
        var sensorId = System.Text.Encoding.UTF8.GetString(payload.Slice(pos, len));
        pos += len;

        if (pos + 8 > payload.Length)
            throw new FormatException("Payload Avro truncado: faltam bytes de value.");
        var value = BinaryPrimitives.ReadDoubleLittleEndian(payload[pos..]);
        pos += 8;

        var measuredAtMillis = ReadZigZag(payload, ref pos);
        var qualityFlag = checked((int)ReadZigZag(payload, ref pos));

        // clock_source é BACKWARD: payload antigo (sem o campo) assume 0 (Unknown).
        // Só lê se ainda há bytes — o codec novo lê o que o codec antigo não escreveu.
        var clockSource = pos < payload.Length ? checked((int)ReadZigZag(payload, ref pos)) : 0;

        return new SensorReadingRecord(
            sensorId, value, DateTimeOffset.FromUnixTimeMilliseconds(measuredAtMillis), qualityFlag, clockSource);
    }

    private static int WriteZigZag(Span<byte> target, long value)
    {
        var encoded = (ulong)((value << 1) ^ (value >> 63));
        var pos = 0;
        while (encoded >= 0x80)
        {
            target[pos++] = (byte)(encoded | 0x80);
            encoded >>= 7;
        }
        target[pos++] = (byte)encoded;
        return pos;
    }

    private static long ReadZigZag(ReadOnlySpan<byte> source, ref int pos)
    {
        ulong result = 0;
        var shift = 0;
        while (true)
        {
            if (pos >= source.Length || shift > 63)
                throw new FormatException("Varint Avro malformado.");
            var b = source[pos++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                break;
            shift += 7;
        }
        return (long)(result >> 1) ^ -(long)(result & 1);
    }
}
