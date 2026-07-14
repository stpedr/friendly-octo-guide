using System.Globalization;

namespace Data.Archiver.Domain.Partitioning;

/// <summary>
/// Nome do objeto no lake: particionado Hive-style por dia/hora (dt=/hour=) — o que
/// engines analíticas (Trino, Spark, DuckDB) entendem sem catálogo. A chave é
/// determinística pelo primeiro offset: replay do Kafka SOBRESCREVE o mesmo objeto
/// em vez de duplicar — at-least-once vira efetivamente-once no lake.
/// </summary>
public static class ArchivePartitioner
{
    public static string ObjectKey(string topic, int partition, long firstOffset, DateTimeOffset firstTimestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentOutOfRangeException.ThrowIfNegative(partition);
        ArgumentOutOfRangeException.ThrowIfNegative(firstOffset);

        var utc = firstTimestamp.ToUniversalTime();
        return string.Create(CultureInfo.InvariantCulture,
            $"{topic}/dt={utc:yyyy-MM-dd}/hour={utc:HH}/part-{partition:D3}-{firstOffset:D12}.jsonl.gz");
    }
}
