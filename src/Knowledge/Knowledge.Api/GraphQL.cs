using System.Security.Claims;
using System.Text.Json;
using Knowledge.Domain.Chunking;
using Knowledge.Domain.Embeddings;

namespace Knowledge.Api;

/// <summary>Consultas GraphQL: busca semântica filtrada pelo RBAC do chamador.</summary>
public sealed class Query
{
    /// <summary>Busca por similaridade de cosseno sobre o índice pgvector.</summary>
    public async Task<IReadOnlyList<SearchHit>> Search(
        string query, int limit, ClaimsPrincipal user,
        KnowledgeStore store, IEmbedder embedder, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var bounded = Math.Clamp(limit, 1, 50);
        var embedding = await embedder.EmbedAsync(query, ct);
        return await store.SearchAsync(embedding, RolesOf(user), bounded, ct);
    }

    internal static List<string> RolesOf(ClaimsPrincipal user) =>
        [.. user.Claims.Where(c => c.Type is "role" or ClaimTypes.Role).Select(c => c.Value)];
}

/// <summary>Mutações GraphQL: ingestão/reindexação de documentos.</summary>
public sealed class Mutation
{
    /// <summary>
    /// Indexa um documento: divide em chunks, gera embeddings e grava tudo numa
    /// transação. Idempotente por id — reenviar reindexa.
    /// </summary>
    public async Task<Guid> IngestDocument(
        Guid? id, string title, string content, IReadOnlyList<string>? visibleToRoles,
        KnowledgeStore store, IEmbedder embedder, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var documentId = id ?? Guid.NewGuid();
        var chunks = DocumentChunker.Split(content);

        var embedded = new List<(Chunk, float[])>(chunks.Count);
        foreach (var chunk in chunks)
            embedded.Add((chunk, await embedder.EmbedAsync(chunk.Content, ct)));

        var metadata = JsonSerializer.Serialize(new { chunkCount = chunks.Count, chars = content.Length });
        await store.IndexAsync(documentId, title, metadata, visibleToRoles ?? [], embedded, ct);
        return documentId;
    }
}
