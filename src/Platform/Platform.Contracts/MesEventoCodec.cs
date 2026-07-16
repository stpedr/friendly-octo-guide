using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.Contracts;

/// <summary>Tipo do evento MES — espelho do enum de schemas/mes-evento.avsc.</summary>
public enum TipoMesEvento { OrdemAberta, OrdemFechada, Apontamento, Defeito, Parada, Setup }

/// <summary>Espelho de schemas/mes-evento.avsc — linha.mes.v1. DTO puro.</summary>
[ExcludeFromCodeCoverage]
public sealed record MesEventoRecord(
    Guid EventId,
    string AtivoId,
    TipoMesEvento Tipo,
    string Codigo,
    double? Quantidade,
    string? Texto,
    string? Turno,
    string SistemaOrigem,
    DateTimeOffset OccurredAt,
    int ClockSource = 0);

/// <summary>
/// Codec do evento MES. Encoding JSON na fase 0 (evento de negócio, baixo volume) —
/// diferente do sensor-reading, que é binário por ser telemetria de alto volume.
/// O contrato executável é este; o Schema Registry entra na fase 1 sem mudar o formato.
/// Fronteira fina de serialização (testada em Mes.Connector.Tests) — fora da conta de
/// cobertura pra não pesar nos serviços que só consomem o sensor-reading.
/// </summary>
[ExcludeFromCodeCoverage]
public static class MesEventoCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Encode(MesEventoRecord evento)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return JsonSerializer.Serialize(evento, Options);
    }

    public static MesEventoRecord Decode(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<MesEventoRecord>(json, Options)
            ?? throw new FormatException("Payload MES vazio ou inválido.");
    }
}
