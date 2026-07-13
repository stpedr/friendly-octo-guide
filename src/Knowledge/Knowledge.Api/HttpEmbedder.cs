using System.Text.Json.Serialization;
using Knowledge.Domain.Embeddings;

namespace Knowledge.Api;

/// <summary>
/// Embedder real: endpoint OpenAI-compatível /v1/embeddings (vLLM, TEI, Ollama...).
/// O modelo e a dimensão são fixos por índice — mudar um exige reindexar tudo.
/// </summary>
public sealed class HttpEmbedder(HttpClient http, string model, int dimensions) : IEmbedder
{
    public int Dimensions { get; } = dimensions;

    public async ValueTask<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync(
            "/v1/embeddings", new EmbeddingRequest(model, [text]), ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(ct)
            ?? throw new InvalidOperationException("Resposta vazia do endpoint de embeddings");
        var embedding = body.Data is [{ Embedding: { } vector }, ..]
            ? vector
            : throw new InvalidOperationException("Endpoint de embeddings não devolveu vetor");

        return embedding.Length == Dimensions
            ? embedding
            : throw new InvalidOperationException(
                $"Modelo devolveu {embedding.Length} dimensões; o índice usa {Dimensions}");
    }

    private sealed record EmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] IReadOnlyList<string> Input);

    private sealed record EmbeddingItem(
        [property: JsonPropertyName("embedding")] float[] Embedding);

    private sealed record EmbeddingResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<EmbeddingItem> Data);
}
