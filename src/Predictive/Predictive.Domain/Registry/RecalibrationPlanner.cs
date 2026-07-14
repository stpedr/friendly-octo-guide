using Predictive.Domain.Scoring;

namespace Predictive.Domain.Registry;

/// <summary>
/// Um run de recalibração pronto pro Model Registry: os hiperparâmetros do detector
/// e as métricas observadas em produção. É o que vira run no MLflow — o versionamento
/// de modelo exigido pela governança (nada de modelo sem proveniência).
/// </summary>
public sealed record RecalibrationRun(
    IReadOnlyDictionary<string, double> Parameters,
    IReadOnlyDictionary<string, double> Metrics);

/// <summary>
/// Decide QUANDO registrar um run e o EMPACOTA. Recalibração só é registrada quando
/// o drift cruzou o limite — run no MLflow é evento de qualidade, não ruído por
/// mensagem. Puro e determinístico.
/// </summary>
public static class RecalibrationPlanner
{
    /// <summary>Registra apenas quando o monitor confirmou drift.</summary>
    public static bool ShouldRegister(DriftReading drift) => drift.Drifting;

    public static RecalibrationRun Build(
        double alpha, double maeThreshold, int warmup, int samples, DriftReading drift)
    {
        ArgumentNullException.ThrowIfNull(drift);
        return new RecalibrationRun(
            Parameters: new Dictionary<string, double>
            {
                ["ewma_alpha"] = alpha,
                ["mae_threshold"] = maeThreshold,
                ["warmup"] = warmup,
            },
            Metrics: new Dictionary<string, double>
            {
                ["mae"] = drift.MeanAbsoluteError,
                ["samples"] = samples,
                ["drift_detected"] = drift.Drifting ? 1 : 0,
            });
    }
}
