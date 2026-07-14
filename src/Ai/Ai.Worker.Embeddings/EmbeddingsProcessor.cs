using System.Net.Http.Json;
using System.Text.Json;
using Ai.Domain.Jobs;
using Ai.Worker.Runtime;

namespace Ai.Worker.Embeddings;

/// <summary>
/// Worker de embeddings: vetoriza texto sobre um endpoint OpenAI-compatível
/// (/v1/embeddings) do GPU pool. Alimenta o RAG do Chatbot/Knowledge e o pgvector —
/// os mesmos vetores que a busca semântica consome. Pod isolado, escala por fila.
/// </summary>
public sealed class EmbeddingsProcessor(HttpClient http, IConfiguration config) : IJobProcessor
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public string ConsumerGroup => "ai-worker-embeddings";
    public string JobsTopic => config["Kafka:EmbeddingJobsTopic"] ?? "ai.jobs.embedding.v1";

    public async Task<object> ProcessAsync(AiJob job, CancellationToken ct)
    {
        EmbeddingRequest request;
        try
        {
            request = JsonSerializer.Deserialize<EmbeddingRequest>(job.Payload, JsonOpts)
                ?? throw new InvalidOperationException("Payload de embedding vazio.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Payload de embedding malformado.", ex);
        }
        if (request.Input is null or [])
            throw new InvalidOperationException("Embedding sem texto de entrada.");

        var body = new
        {
            model = config["Embeddings:Model"] ?? "nomic-embed-text",
            input = request.Input,
        };

        using var response = await http.PostAsJsonAsync("/v1/embeddings", body, JsonOpts, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("Endpoint de embeddings devolveu corpo vazio.");
        var vectors = result.Data?.Select(d => d.Embedding).ToArray() ?? [];
        return new { count = vectors.Length, dimensions = vectors.FirstOrDefault()?.Length ?? 0, vectors };
    }

    private sealed record EmbeddingRequest(IReadOnlyList<string>? Input);
    private sealed record EmbeddingResponse(IReadOnlyList<EmbeddingItem>? Data);
    private sealed record EmbeddingItem(float[] Embedding);
}
