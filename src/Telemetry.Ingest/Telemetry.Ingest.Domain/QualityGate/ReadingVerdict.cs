namespace Telemetry.Ingest.Domain.QualityGate;

public enum RejectionReason
{
    None,
    OutOfPhysicalRange,   // fora do envelope físico do sensor (ex.: -400°C)
    UnsyncedClock,        // relógio de origem não confiável (sem NTP/PTP) — timestamp não vale
    ClockDriftExceeded,   // relógio do dispositivo à frente/atrás demais do servidor
    StaleReading,         // medida antiga demais pra valer como "estado atual"
}

/// <summary>
/// Veredito do quality gate. Leitura rejeitada NUNCA é descartada —
/// vai pro tópico de quarentena com o motivo, preservando a garantia de não-perda.
/// </summary>
public sealed record ReadingVerdict(bool Accepted, RejectionReason Reason)
{
    public static readonly ReadingVerdict Ok = new(true, RejectionReason.None);
    public static ReadingVerdict Rejected(RejectionReason reason) => new(false, reason);
}
