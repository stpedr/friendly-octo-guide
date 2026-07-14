namespace Edge.ProtocolGateway.Domain.Translation;

/// <summary>Como o dado bruto de um registrador é decodificado antes da escala.</summary>
public enum RegisterType
{
    Unsigned16,   // 1 registrador, sem sinal
    Signed16,     // 1 registrador, com sinal
    Signed32,     // 2 registradores (big-endian de words)
    Real32,       // 2 registradores IEEE-754 float (big-endian de words)
}

/// <summary>
/// Como um registrador Modbus vira grandeza física: decodifica o tipo, depois
/// valor_bruto * escala + offset. O mapa é o "dicionário" da linha — versionado
/// junto com o código do gateway.
/// </summary>
public sealed record RegisterMapping(
    string SensorId, ushort Address, double Scale, double Offset,
    RegisterType Type = RegisterType.Unsigned16);

/// <summary>
/// Mapeia um nó OPC-UA (NodeId, ex. "ns=2;s=Prensa3.Temp") pro sensor interno,
/// com escala/offset — o equivalente do RegisterMapping pro mundo OPC-UA.
/// </summary>
public sealed record OpcUaMapping(string SensorId, string NodeId, double Scale, double Offset);

/// <summary>Evento interno canônico — o MESMO formato que o Kafka transporta (schemas/sensor-reading.avsc).</summary>
public sealed record LineEvent(string SensorId, double Value, DateTimeOffset MeasuredAt);

/// <summary>
/// Traduz o mundo OT (registradores Modbus, tópicos MQTT de PLC) pro evento interno.
/// Endereço não mapeado é recusado na hora — dado de origem desconhecida não entra
/// no pipeline nem com integridade perfeita.
/// </summary>
public sealed class ProtocolTranslator
{
    private readonly Dictionary<ushort, RegisterMapping> _byAddress;
    private readonly Dictionary<string, OpcUaMapping> _byNodeId;

    public ProtocolTranslator(IReadOnlyList<RegisterMapping> registerMap, IReadOnlyList<OpcUaMapping>? opcUaMap = null)
    {
        _byAddress = registerMap.ToDictionary(m => m.Address);
        _byNodeId = (opcUaMap ?? []).ToDictionary(m => m.NodeId, StringComparer.Ordinal);
    }

    /// <summary>Registrador único de 16 bits (compat.: UInt16/Int16 conforme o mapa).</summary>
    public LineEvent TranslateModbus(ushort address, ushort rawValue, DateTimeOffset measuredAt) =>
        TranslateModbus(address, [rawValue], measuredAt);

    /// <summary>
    /// Um ou dois registradores (16/32 bits, int ou float IEEE-754). O tipo vem do
    /// mapa; a quantidade de words tem que bater com ele — dado do tamanho errado é
    /// recusado em vez de virar leitura silenciosamente errada.
    /// </summary>
    public LineEvent TranslateModbus(ushort address, IReadOnlyList<ushort> words, DateTimeOffset measuredAt)
    {
        if (!_byAddress.TryGetValue(address, out var mapping))
            throw new UnknownSourceException($"Registrador Modbus {address} não está no mapa da linha.");

        var raw = Decode(mapping.Type, words, address);
        return new LineEvent(mapping.SensorId, raw * mapping.Scale + mapping.Offset, measuredAt);
    }

    /// <summary>Valor lido de um nó OPC-UA já como número — escala/offset do mapa.</summary>
    public LineEvent TranslateOpcUa(string nodeId, double rawValue, DateTimeOffset measuredAt)
    {
        if (!_byNodeId.TryGetValue(nodeId, out var mapping))
            throw new UnknownSourceException($"Nó OPC-UA '{nodeId}' não está no mapa da linha.");

        return new LineEvent(mapping.SensorId, rawValue * mapping.Scale + mapping.Offset, measuredAt);
    }

    private static double Decode(RegisterType type, IReadOnlyList<ushort> w, ushort address)
    {
        var need = type is RegisterType.Signed32 or RegisterType.Real32 ? 2 : 1;
        if (w.Count != need)
            throw new UnknownSourceException(
                $"Registrador {address} ({type}) espera {need} word(s), recebeu {w.Count}.");

        return type switch
        {
            RegisterType.Unsigned16 => w[0],
            RegisterType.Signed16 => unchecked((short)w[0]),
            // Big-endian de words: primeiro registrador é o mais significativo.
            RegisterType.Signed32 => unchecked((int)(((uint)w[0] << 16) | w[1])),
            RegisterType.Real32 => BitConverter.UInt32BitsToSingle(((uint)w[0] << 16) | w[1]),
            _ => throw new UnknownSourceException($"Tipo de registrador não suportado: {type}."),
        };
    }

    /// <summary>Tópico MQTT padrão da linha: linha/{linha}/sensor/{sensorId}.</summary>
    public static string? SensorIdFromTopic(string topic)
    {
        var parts = topic.Split('/');
        return parts is ["linha", _, "sensor", var sensorId] ? sensorId : null;
    }
}

public sealed class UnknownSourceException(string message) : Exception(message);
