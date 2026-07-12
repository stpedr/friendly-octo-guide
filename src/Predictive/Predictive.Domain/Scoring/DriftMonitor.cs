namespace Predictive.Domain.Scoring;

public sealed record DriftReading(double MeanAbsoluteError, bool Drifting);

/// <summary>
/// Predição vs. real: erro absoluto médio em janela exponencial. MAE acima do
/// aceitável = o modelo derivou do processo (drift) — retreino é acionado por
/// alerta, não por palpite. Métrica exigida pela governança: modelo não é
/// determinístico, então a qualidade é vigiada em produção, não só no eval.
/// </summary>
public sealed class DriftMonitor(double alpha = 0.1, double maeThreshold = 1.0)
{
    private double _mae;
    private int _samples;

    public DriftReading Compare(double predicted, double actual)
    {
        var error = Math.Abs(predicted - actual);
        _samples++;
        _mae = _samples == 1 ? error : _mae + alpha * (error - _mae);
        return new DriftReading(_mae, _samples >= 10 && _mae > maeThreshold);
    }
}
