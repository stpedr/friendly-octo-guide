namespace Edge.ProtocolGateway.Domain.Translation;

/// <summary>
/// Como um registrador Modbus vira grandeza física: valor_bruto * escala + offset.
/// O mapa é o "dicionário" da linha — versionado junto com o código do gateway.
/// </summary>
public sealed record RegisterMapping(string SensorId, ushort Address, double Scale, double Offset);

/// <summary>Evento interno canônico — o MESMO formato que o Kafka transporta (schemas/sensor-reading.avsc).</summary>
public sealed record LineEvent(string SensorId, double Value, DateTimeOffset MeasuredAt);

/// <summary>
/// Traduz o mundo OT (registradores Modbus, tópicos MQTT de PLC) pro evento interno.
/// Endereço não mapeado é recusado na hora — dado de origem desconhecida não entra
/// no pipeline nem com integridade perfeita.
/// </summary>
public sealed class ProtocolTranslator(IReadOnlyList<RegisterMapping> registerMap)
{
    private readonly Dictionary<ushort, RegisterMapping> _byAddress =
        registerMap.ToDictionary(m => m.Address);

    public LineEvent TranslateModbus(ushort address, ushort rawValue, DateTimeOffset measuredAt)
    {
        if (!_byAddress.TryGetValue(address, out var mapping))
            throw new UnknownSourceException($"Registrador Modbus {address} não está no mapa da linha.");

        return new LineEvent(mapping.SensorId, rawValue * mapping.Scale + mapping.Offset, measuredAt);
    }

    /// <summary>Tópico MQTT padrão da linha: linha/{linha}/sensor/{sensorId}.</summary>
    public static string? SensorIdFromTopic(string topic)
    {
        var parts = topic.Split('/');
        return parts is ["linha", _, "sensor", var sensorId] ? sensorId : null;
    }
}

public sealed class UnknownSourceException(string message) : Exception(message);
