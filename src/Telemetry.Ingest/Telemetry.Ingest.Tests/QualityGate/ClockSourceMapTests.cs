using Telemetry.Ingest.Domain.QualityGate;
using Xunit;

namespace Telemetry.Ingest.Tests.QualityGate;

public class ClockSourceMapTests
{
    [Theory]
    [InlineData(0, ClockSource.Unknown)]
    [InlineData(1, ClockSource.Ntp)]
    [InlineData(2, ClockSource.Ptp)]
    [InlineData(3, ClockSource.Unsynced)]
    [InlineData(99, ClockSource.Unknown)]   // valor desconhecido = não confiável
    [InlineData(-1, ClockSource.Unknown)]
    public void FromWire_mapeia_o_int_do_contrato(int wire, ClockSource expected)
    {
        Assert.Equal(expected, ClockSourceMap.FromWire(wire));
    }
}
