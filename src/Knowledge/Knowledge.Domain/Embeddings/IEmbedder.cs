namespace Knowledge.Domain.Embeddings;

/// <summary>
/// Gera o vetor de um texto. A implementação real chama um modelo (endpoint
/// OpenAI-compatível /v1/embeddings); a de dev é local e determinística.
/// A dimensão é fixa por índice — trocar de modelo implica reindexar.
/// </summary>
public interface IEmbedder
{
    int Dimensions { get; }
    ValueTask<float[]> EmbedAsync(string text, CancellationToken ct = default);
}
