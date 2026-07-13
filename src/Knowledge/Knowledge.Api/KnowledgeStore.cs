using System.Globalization;
using System.Text;
using Knowledge.Domain.Chunking;
using Npgsql;

namespace Knowledge.Api;

/// <summary>Resultado de busca semântica: chunk + documento de origem + score de cosseno.</summary>
public sealed record SearchHit(Guid DocumentId, string Title, string Content, double Score);

/// <summary>
/// Persistência do índice: documentos (metadados em JSONB) + chunks com embedding
/// pgvector. Documento e chunks entram na mesma transação — nunca existe documento
/// meio-indexado. A visibilidade por papel é filtrada NA QUERY: chunk que o usuário
/// não pode ver nem sai do banco.
/// </summary>
public sealed class KnowledgeStore(string connectionString, int dimensions)
{
    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            $$"""
            CREATE EXTENSION IF NOT EXISTS vector;
            CREATE TABLE IF NOT EXISTS documents (
                id UUID PRIMARY KEY,
                title TEXT NOT NULL,
                metadata JSONB NOT NULL DEFAULT '{}',
                visible_to_roles TEXT[] NOT NULL DEFAULT '{}',
                created_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );
            CREATE TABLE IF NOT EXISTS chunks (
                id UUID PRIMARY KEY,
                document_id UUID NOT NULL REFERENCES documents ON DELETE CASCADE,
                seq INT NOT NULL,
                content TEXT NOT NULL,
                embedding vector({{dimensions}}) NOT NULL
            );
            CREATE INDEX IF NOT EXISTS chunks_embedding ON chunks
                USING hnsw (embedding vector_cosine_ops);
            """, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Indexa (ou reindexa) um documento inteiro numa transação.</summary>
    public async Task IndexAsync(
        Guid id, string title, string metadataJson, IReadOnlyList<string> visibleToRoles,
        IReadOnlyList<(Chunk Chunk, float[] Embedding)> chunks, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var cmd = new NpgsqlCommand(
            """
            INSERT INTO documents (id, title, metadata, visible_to_roles) VALUES ($1, $2, $3::jsonb, $4)
            ON CONFLICT (id) DO UPDATE SET title = $2, metadata = $3::jsonb, visible_to_roles = $4
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue(id);
            cmd.Parameters.AddWithValue(title);
            cmd.Parameters.AddWithValue(metadataJson);
            cmd.Parameters.AddWithValue(visibleToRoles.ToArray());
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var del = new NpgsqlCommand("DELETE FROM chunks WHERE document_id = $1", conn, tx))
        {
            del.Parameters.AddWithValue(id);
            await del.ExecuteNonQueryAsync(ct);
        }

        foreach (var (chunk, embedding) in chunks)
        {
            await using var ins = new NpgsqlCommand(
                "INSERT INTO chunks (id, document_id, seq, content, embedding) VALUES ($1, $2, $3, $4, $5::vector)",
                conn, tx);
            ins.Parameters.AddWithValue(Guid.NewGuid());
            ins.Parameters.AddWithValue(id);
            ins.Parameters.AddWithValue(chunk.Sequence);
            ins.Parameters.AddWithValue(chunk.Content);
            ins.Parameters.AddWithValue(ToVectorLiteral(embedding));
            await ins.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Busca por cosseno. Documento sem restrição (array vazio) é visível a qualquer
    /// autenticado; com restrição, só a quem tem interseção de papéis.
    /// </summary>
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        float[] queryEmbedding, IReadOnlyList<string> callerRoles, int limit, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT d.id, d.title, c.content, 1 - (c.embedding <=> $1::vector) AS score
            FROM chunks c
            JOIN documents d ON d.id = c.document_id
            WHERE cardinality(d.visible_to_roles) = 0 OR d.visible_to_roles && $2
            ORDER BY c.embedding <=> $1::vector
            LIMIT $3
            """, conn);
        cmd.Parameters.AddWithValue(ToVectorLiteral(queryEmbedding));
        cmd.Parameters.AddWithValue(callerRoles.ToArray());
        cmd.Parameters.AddWithValue(limit);

        var hits = new List<SearchHit>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            hits.Add(new SearchHit(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetDouble(3)));
        }
        return hits;
    }

    // Literal textual '[x,y,...]' com cast ::vector — dispensa plugin de tipo no driver.
    private static string ToVectorLiteral(float[] embedding)
    {
        var sb = new StringBuilder(embedding.Length * 12).Append('[');
        for (var i = 0; i < embedding.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append(embedding[i].ToString("G9", CultureInfo.InvariantCulture));
        }
        return sb.Append(']').ToString();
    }
}
