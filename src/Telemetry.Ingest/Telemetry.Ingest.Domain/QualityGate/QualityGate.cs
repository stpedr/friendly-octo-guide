namespace Telemetry.Ingest.Domain.QualityGate;

/// <summary>Envelope físico permitido para um tipo de sensor.</summary>
public sealed record SensorLimits(double Min, double Max);

/// <summary>
/// Valida a leitura ANTES de ela virar "verdade" no Postgres/Big Data Pool.
/// Regras na ordem de custo: range físico → clock drift → staleness.
/// Classe pura e determinística: o relógio entra por parâmetro, não por DateTime.Now.
/// </summary>
public sealed class QualityGate(
    IReadOnlyDictionary<string, SensorLimits> limitsBySensor,
    TimeSpan maxClockDrift,
    TimeSpan maxStaleness)
{
    public ReadingVerdict Evaluate(SensorReading reading)
    {
        if (!limitsBySensor.TryGetValue(reading.SensorId, out var limits))
            // Sensor não cadastrado = fora do envelope por definição:
            // aceitar dado de origem desconhecida quebraria a auditabilidade.
            return ReadingVerdict.Rejected(RejectionReason.OutOfPhysicalRange);

        if (double.IsNaN(reading.Value) || reading.Value < limits.Min || reading.Value > limits.Max)
            return ReadingVerdict.Rejected(RejectionReason.OutOfPhysicalRange);

        var drift = reading.MeasuredAt - reading.ReceivedAt;
        if (drift > maxClockDrift)
            return ReadingVerdict.Rejected(RejectionReason.ClockDriftExceeded);

        if (-drift > maxStaleness)
            return ReadingVerdict.Rejected(RejectionReason.StaleReading);

        return ReadingVerdict.Ok;
    }
}
