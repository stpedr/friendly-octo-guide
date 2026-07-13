using Data.Archiver.Domain.Batching;
using Xunit;

namespace Data.Archiver.Tests;

public class ArchiveBatcherTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Lote_vazio_nunca_pede_flush()
    {
        var batcher = new ArchiveBatcher(maxRecords: 10, maxBytes: 1024, maxAge: TimeSpan.FromSeconds(1));
        Assert.False(batcher.ShouldFlush(T0.AddHours(1)));
    }

    [Fact]
    public void Flush_por_contagem()
    {
        var batcher = new ArchiveBatcher(maxRecords: 2, maxBytes: long.MaxValue, maxAge: TimeSpan.MaxValue);
        batcher.Add("a", T0);
        Assert.False(batcher.ShouldFlush(T0));
        batcher.Add("b", T0);
        Assert.True(batcher.ShouldFlush(T0));
    }

    [Fact]
    public void Flush_por_bytes()
    {
        var batcher = new ArchiveBatcher(maxRecords: int.MaxValue, maxBytes: 10, maxAge: TimeSpan.MaxValue);
        batcher.Add("123456789", T0); // 9 + \n = 10 bytes
        Assert.True(batcher.ShouldFlush(T0));
    }

    [Fact]
    public void Flush_por_idade_conta_a_partir_da_primeira_linha()
    {
        var batcher = new ArchiveBatcher(maxRecords: int.MaxValue, maxBytes: long.MaxValue, maxAge: TimeSpan.FromSeconds(30));
        batcher.Add("a", T0);
        Assert.False(batcher.ShouldFlush(T0.AddSeconds(29)));
        Assert.True(batcher.ShouldFlush(T0.AddSeconds(30)));
    }

    [Fact]
    public void Drain_devolve_na_ordem_e_zera_o_estado()
    {
        var batcher = new ArchiveBatcher(maxRecords: 2, maxBytes: long.MaxValue, maxAge: TimeSpan.MaxValue);
        batcher.Add("a", T0);
        batcher.Add("b", T0);

        Assert.Equal(["a", "b"], batcher.Drain());
        Assert.True(batcher.IsEmpty);
        Assert.False(batcher.ShouldFlush(T0.AddYears(1))); // idade zerou junto
    }
}
