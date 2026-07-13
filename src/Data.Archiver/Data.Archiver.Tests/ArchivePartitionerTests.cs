using Data.Archiver.Domain.Partitioning;
using Xunit;

namespace Data.Archiver.Tests;

public class ArchivePartitionerTests
{
    [Fact]
    public void Chave_e_hive_style_por_dia_e_hora_em_utc()
    {
        var key = ArchivePartitioner.ObjectKey(
            "linha.telemetria.v1", partition: 2, firstOffset: 12345,
            firstTimestamp: new DateTimeOffset(2026, 7, 13, 1, 30, 0, TimeSpan.FromHours(-3)));

        // 01:30-03:00 = 04:30 UTC — a partição segue o relógio do lake, não o local.
        Assert.Equal("linha.telemetria.v1/dt=2026-07-13/hour=04/part-002-000000012345.jsonl.gz", key);
    }

    [Fact]
    public void Mesmo_primeiro_offset_gera_a_mesma_chave_para_replay_sobrescrever()
    {
        var ts = new DateTimeOffset(2026, 7, 13, 4, 0, 0, TimeSpan.Zero);
        Assert.Equal(
            ArchivePartitioner.ObjectKey("t", 0, 7, ts),
            ArchivePartitioner.ObjectKey("t", 0, 7, ts));
    }
}
