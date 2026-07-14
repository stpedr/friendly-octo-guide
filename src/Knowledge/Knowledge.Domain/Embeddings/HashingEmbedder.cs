namespace Knowledge.Domain.Embeddings;

/// <summary>
/// Embedding local por feature hashing (bag-of-words → FNV-1a → dimensão), com
/// normalização L2. Não entende semântica — é o embedder de dev/fallback que
/// mantém o pipeline inteiro funcional sem GPU: mesmo texto → mesmo vetor,
/// textos com vocabulário parecido → vetores próximos em cosseno.
/// </summary>
public sealed class HashingEmbedder(int dimensions = 384) : IEmbedder
{
    public int Dimensions { get; } = dimensions;

    public ValueTask<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        var vector = new float[Dimensions];
        var tokens = text.ToUpperInvariant().Split(
            [' ', ',', '.', ';', ':', '?', '!', '\n', '\t', '(', ')', '"', '\''],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            var hash = Fnv1a(token);
            // Bit extra do hash decide o sinal — espalha melhor que só somar.
            var sign = (hash & 0x8000_0000u) == 0 ? 1f : -1f;
            vector[(int)(hash % (uint)Dimensions)] += sign;
        }

        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm > 0)
        {
            for (var i = 0; i < vector.Length; i++)
                vector[i] /= norm;
        }

        return ValueTask.FromResult(vector);
    }

    // FNV-1a estável entre processos — string.GetHashCode é randomizado por design.
    private static uint Fnv1a(string token)
    {
        var hash = 2166136261u;
        foreach (var c in token)
        {
            hash ^= c;
            hash *= 16777619u;
        }
        return hash;
    }
}
