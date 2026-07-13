namespace Data.Archiver.Domain.Batching;

/// <summary>
/// Acumula linhas até valer a pena escrever um objeto no data lake: por contagem,
/// por bytes ou por idade — o que estourar primeiro. Objeto pequeno demais degrada
/// leitura analítica; segurar demais atrasa o lake. Determinístico: relógio por parâmetro.
/// </summary>
public sealed class ArchiveBatcher(int maxRecords, long maxBytes, TimeSpan maxAge)
{
    private readonly List<string> _lines = [];
    private long _bytes;
    private DateTimeOffset? _openedAt;

    public int Count => _lines.Count;
    public bool IsEmpty => _lines.Count == 0;

    public void Add(string line, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(line);
        _openedAt ??= now;
        _lines.Add(line);
        _bytes += System.Text.Encoding.UTF8.GetByteCount(line) + 1; // +1 = \n
    }

    public bool ShouldFlush(DateTimeOffset now) =>
        !IsEmpty && (_lines.Count >= maxRecords
                  || _bytes >= maxBytes
                  || now - _openedAt >= maxAge);

    /// <summary>Devolve o lote e zera o estado — pronto pro próximo objeto.</summary>
    public IReadOnlyList<string> Drain()
    {
        var drained = _lines.ToArray();
        _lines.Clear();
        _bytes = 0;
        _openedAt = null;
        return drained;
    }
}
