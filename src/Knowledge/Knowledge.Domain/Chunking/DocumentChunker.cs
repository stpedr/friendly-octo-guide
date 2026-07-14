namespace Knowledge.Domain.Chunking;

/// <summary>Pedaço indexável de um documento, na ordem original.</summary>
public sealed record Chunk(int Sequence, string Content);

/// <summary>
/// Divide um documento em chunks pro índice vetorial. Corta por parágrafo (a
/// fronteira semântica natural) e só quebra no meio quando um parágrafo sozinho
/// estoura o teto — com sobreposição, pra busca não perder o que ficou na emenda.
/// Determinístico: mesmo texto, mesmos chunks.
/// </summary>
public static class DocumentChunker
{
    public const int DefaultMaxChars = 1200;
    public const int DefaultOverlapChars = 150;

    public static IReadOnlyList<Chunk> Split(
        string content, int maxChars = DefaultMaxChars, int overlapChars = DefaultOverlapChars)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxChars, 100);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(overlapChars, maxChars);

        var paragraphs = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var chunks = new List<Chunk>();
        var current = new System.Text.StringBuilder();

        void Flush()
        {
            if (current.Length == 0)
                return;
            chunks.Add(new Chunk(chunks.Count, current.ToString()));
            current.Clear();
        }

        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Length > maxChars)
            {
                // Parágrafo gigante: fecha o acumulado e fatia com sobreposição.
                Flush();
                var start = 0;
                while (start < paragraph.Length)
                {
                    var length = Math.Min(maxChars, paragraph.Length - start);
                    chunks.Add(new Chunk(chunks.Count, paragraph.Substring(start, length)));
                    if (start + length >= paragraph.Length)
                        break;
                    start += maxChars - overlapChars;
                }
                continue;
            }

            if (current.Length > 0 && current.Length + paragraph.Length + 2 > maxChars)
                Flush();

            if (current.Length > 0)
                current.Append("\n\n");
            current.Append(paragraph);
        }

        Flush();
        return chunks;
    }
}
