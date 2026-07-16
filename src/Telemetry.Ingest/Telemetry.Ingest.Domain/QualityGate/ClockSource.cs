namespace Telemetry.Ingest.Domain.QualityGate;

/// <summary>
/// Fonte/qualidade do relógio que carimbou a leitura. Sem sincronização confiável,
/// o instante da medição não vale como verdade — drift e staleness perdem o sentido.
/// O valor cru vem do contrato (clock_source em schemas/sensor-reading.avsc).
/// </summary>
public enum ClockSource
{
    Unknown = 0,   // não declarado — trata como não confiável
    Ntp = 1,       // sincronizado por NTP (chrony)
    Ptp = 2,       // sincronizado por PTP (IEEE 1588, sub-milissegundo)
    Unsynced = 3,  // dispositivo declarou explicitamente que NÃO está sincronizado
}

public static class ClockSourceMap
{
    /// <summary>Mapeia o int cru do contrato pro enum; valor desconhecido = Unknown.</summary>
    public static ClockSource FromWire(int wire) => wire switch
    {
        1 => ClockSource.Ntp,
        2 => ClockSource.Ptp,
        3 => ClockSource.Unsynced,
        _ => ClockSource.Unknown,
    };
}
