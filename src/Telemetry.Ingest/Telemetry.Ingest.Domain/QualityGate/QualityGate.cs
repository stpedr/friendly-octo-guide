namespace Telemetry.Ingest.Domain.QualityGate;

/// <summary>Envelope físico permitido para um tipo de sensor.</summary>
public sealed record SensorLimits(double Min, double Max);

/// <summary>
/// Valida a leitura ANTES de ela virar "verdade" no Postgres/Big Data Pool.
/// Regras na ordem de custo: range físico → confiança do relógio → clock drift →
/// staleness. Classe pura e determinística: o relógio entra por parâmetro, não por
/// DateTime.Now. <paramref name="requireSyncedClock"/> fica off por padrão até o edge
/// popular a fonte de relógio (clock_source) — ligá-lo antes disso rejeitaria tudo.
/// </summary>
public sealed class QualityGate(
    IReadOnlyDictionary<string, SensorLimits> limitsBySensor,
    TimeSpan maxClockDrift,
    TimeSpan maxStaleness,
    bool requireSyncedClock = false)
{
    public ReadingVerdict Evaluate(SensorReading reading)
    {
        if (!limitsBySensor.TryGetValue(reading.SensorId, out var limits))
            // Sensor não cadastrado = fora do envelope por definição:
            // aceitar dado de origem desconhecida quebraria a auditabilidade.
            return ReadingVerdict.Rejected(RejectionReason.OutOfPhysicalRange);

        if (double.IsNaN(reading.Value) || reading.Value < limits.Min || reading.Value > limits.Max)
            return ReadingVerdict.Rejected(RejectionReason.OutOfPhysicalRange);

        // Relógio não confiável invalida drift e staleness — checa antes deles:
        // preservar um timestamp de origem dessincronizada é preservar lixo com
        // integridade perfeita.
        if (requireSyncedClock && reading.ClockSource is ClockSource.Unsynced or ClockSource.Unknown)
            return ReadingVerdict.Rejected(RejectionReason.UnsyncedClock);

        var drift = reading.MeasuredAt - reading.ReceivedAt;
        if (drift > maxClockDrift)
            return ReadingVerdict.Rejected(RejectionReason.ClockDriftExceeded);

        if (-drift > maxStaleness)
            return ReadingVerdict.Rejected(RejectionReason.StaleReading);

        return ReadingVerdict.Ok;
    }
}
