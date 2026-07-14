using Predictive.Domain.Registry;
using Predictive.Domain.Scoring;
using Xunit;

namespace Predictive.Tests.Registry;

public class RecalibrationPlannerTests
{
    [Fact]
    public void So_registra_run_quando_ha_drift()
    {
        Assert.True(RecalibrationPlanner.ShouldRegister(new DriftReading(2.0, Drifting: true)));
        Assert.False(RecalibrationPlanner.ShouldRegister(new DriftReading(0.1, Drifting: false)));
    }

    [Fact]
    public void Run_empacota_hiperparametros_e_metricas()
    {
        var run = RecalibrationPlanner.Build(
            alpha: 0.1, maeThreshold: 1.0, warmup: 30, samples: 500,
            drift: new DriftReading(1.7, Drifting: true));

        Assert.Equal(0.1, run.Parameters["ewma_alpha"]);
        Assert.Equal(1.0, run.Parameters["mae_threshold"]);
        Assert.Equal(30, run.Parameters["warmup"]);
        Assert.Equal(1.7, run.Metrics["mae"]);
        Assert.Equal(500, run.Metrics["samples"]);
        Assert.Equal(1, run.Metrics["drift_detected"]); // drift virou 1.0
    }
}
