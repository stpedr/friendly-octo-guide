using Platform.Contracts;
using Telemetry.Ingest.Domain.QualityGate;
using Xunit;
using Gate = Telemetry.Ingest.Domain.QualityGate.QualityGate;

namespace Telemetry.Ingest.Tests.QualityGate;

/// <summary>
/// Contrato → domínio: o que o edge codifica em Avro chega ao quality gate
/// com a mesma semântica — sensor, valor e instante de medição intactos.
/// </summary>
public class ContractToDomainTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);

    private static readonly Gate Gate = new(
        limitsBySensor: new Dictionary<string, SensorLimits> { ["temp-forno-01"] = new(-40, 900) },
        maxClockDrift: TimeSpan.FromSeconds(5),
        maxStaleness: TimeSpan.FromMinutes(10));

    private static SensorReading DecodeAsDomain(byte[] wire, DateTimeOffset receivedAt)
    {
        var record = SensorReadingCodec.Decode(wire);
        return new SensorReading(record.SensorId, record.Value, record.MeasuredAt, receivedAt);
    }

    [Fact]
    public void Leitura_valida_do_edge_e_aceita_apos_o_roundtrip_avro()
    {
        var wire = SensorReadingCodec.Encode(new SensorReadingRecord("temp-forno-01", 812.5, Now));
        var reading = DecodeAsDomain(wire, receivedAt: Now + TimeSpan.FromSeconds(2));

        Assert.True(Gate.Evaluate(reading).Accepted);
    }

    [Fact]
    public void Leitura_fora_do_envelope_fisico_e_rejeitada_apos_o_roundtrip()
    {
        var wire = SensorReadingCodec.Encode(new SensorReadingRecord("temp-forno-01", 2500, Now));
        var reading = DecodeAsDomain(wire, receivedAt: Now + TimeSpan.FromSeconds(2));

        var verdict = Gate.Evaluate(reading);
        Assert.False(verdict.Accepted);
        Assert.Equal(RejectionReason.OutOfPhysicalRange, verdict.Reason);
    }

    [Fact]
    public void Timestamp_em_milissegundos_nao_perde_precisao_no_gate_de_staleness()
    {
        var measuredAt = Now - TimeSpan.FromMinutes(9);
        var wire = SensorReadingCodec.Encode(new SensorReadingRecord("temp-forno-01", 100, measuredAt));
        var reading = DecodeAsDomain(wire, receivedAt: Now);

        Assert.True(Gate.Evaluate(reading).Accepted); // 9 min < 10 min de staleness máxima
    }
}
