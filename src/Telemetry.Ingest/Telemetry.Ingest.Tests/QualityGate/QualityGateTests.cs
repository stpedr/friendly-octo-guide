using Telemetry.Ingest.Domain.QualityGate;
using Xunit;
using Gate = Telemetry.Ingest.Domain.QualityGate.QualityGate;

namespace Telemetry.Ingest.Tests.QualityGate;

// Este arquivo é o exemplo vivo do ciclo red→green→refactor do repo:
// cada regra do gate nasceu de um teste aqui antes de existir em QualityGate.cs.
public class QualityGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    private static Gate DefaultGate() => new(
        limitsBySensor: new Dictionary<string, SensorLimits>
        {
            ["temp-forno-01"] = new(Min: -40, Max: 900),
        },
        maxClockDrift: TimeSpan.FromSeconds(5),
        maxStaleness: TimeSpan.FromMinutes(10));

    private static SensorReading Reading(
        string sensorId = "temp-forno-01",
        double value = 250,
        TimeSpan? measuredOffset = null)
        => new(sensorId, value, Now + (measuredOffset ?? TimeSpan.Zero), Now);

    [Fact]
    public void Aceita_leitura_dentro_do_envelope_e_com_relogio_sincronizado()
    {
        var verdict = DefaultGate().Evaluate(Reading());

        Assert.True(verdict.Accepted);
        Assert.Equal(RejectionReason.None, verdict.Reason);
    }

    [Theory]
    [InlineData(-41)]     // abaixo do mínimo físico
    [InlineData(901)]     // acima do máximo físico
    [InlineData(double.NaN)]
    public void Rejeita_valor_fora_do_envelope_fisico(double value)
    {
        var verdict = DefaultGate().Evaluate(Reading(value: value));

        Assert.False(verdict.Accepted);
        Assert.Equal(RejectionReason.OutOfPhysicalRange, verdict.Reason);
    }

    [Fact]
    public void Aceita_valores_exatamente_nos_limites()
    {
        var gate = DefaultGate();

        Assert.True(gate.Evaluate(Reading(value: -40)).Accepted);
        Assert.True(gate.Evaluate(Reading(value: 900)).Accepted);
    }

    [Fact]
    public void Rejeita_sensor_nao_cadastrado()
    {
        var verdict = DefaultGate().Evaluate(Reading(sensorId: "sensor-fantasma"));

        Assert.False(verdict.Accepted);
        Assert.Equal(RejectionReason.OutOfPhysicalRange, verdict.Reason);
    }

    [Fact]
    public void Rejeita_relogio_do_dispositivo_adiantado_alem_do_drift_maximo()
    {
        // dispositivo diz que mediu 6s "no futuro" em relação ao servidor
        var verdict = DefaultGate().Evaluate(Reading(measuredOffset: TimeSpan.FromSeconds(6)));

        Assert.False(verdict.Accepted);
        Assert.Equal(RejectionReason.ClockDriftExceeded, verdict.Reason);
    }

    [Fact]
    public void Aceita_drift_no_limite_exato()
    {
        var verdict = DefaultGate().Evaluate(Reading(measuredOffset: TimeSpan.FromSeconds(5)));

        Assert.True(verdict.Accepted);
    }

    [Fact]
    public void Rejeita_leitura_antiga_demais_como_stale()
    {
        // store-and-forward legítimo chega atrasado — mas acima de 10 min não vale como "agora"
        var verdict = DefaultGate().Evaluate(Reading(measuredOffset: TimeSpan.FromMinutes(-11)));

        Assert.False(verdict.Accepted);
        Assert.Equal(RejectionReason.StaleReading, verdict.Reason);
    }

    [Fact]
    public void Aceita_atraso_de_store_and_forward_dentro_da_janela()
    {
        var verdict = DefaultGate().Evaluate(Reading(measuredOffset: TimeSpan.FromMinutes(-9)));

        Assert.True(verdict.Accepted);
    }
}
