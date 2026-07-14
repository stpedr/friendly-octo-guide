using Knowledge.Api;
using Knowledge.Domain.Chunking;
using Knowledge.Domain.Embeddings;
using Xunit;

namespace Knowledge.IntegrationTests;

/// <summary>
/// Integração REAL contra Postgres + pgvector (não fake): cria o schema de verdade
/// (extensão vector, índice HNSW), indexa documentos, e faz busca por cosseno com
/// o filtro de visibilidade por papel — o caminho inteiro do bloco de dados
/// não-relacional exercitado num banco de verdade.
///
/// Roda só com a env var KNOWLEDGE_PG apontando pro banco (ex.:
/// "Host=localhost;Username=postgres;Password=dev;Database=knowledge"); sem ela,
/// os testes se auto-pulam pra não quebrar o CID padrão sem infra.
/// </summary>
public sealed class PgVectorLiveTests
{
    private static string? Conn => Environment.GetEnvironmentVariable("KNOWLEDGE_PG");
    private const int Dims = 64;
    private static readonly HashingEmbedder Embedder = new(Dims);

    private static async Task<KnowledgeStore> FreshStoreAsync()
    {
        var store = new KnowledgeStore(Conn!, Dims);
        await store.EnsureSchemaAsync(CancellationToken.None);
        // Isola cada teste: limpa o que ficou de execuções anteriores.
        await using var conn = new Npgsql.NpgsqlConnection(Conn);
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand("TRUNCATE documents CASCADE;", conn);
        await cmd.ExecuteNonQueryAsync();
        return store;
    }

    private static async Task IndexAsync(KnowledgeStore store, Guid id, string title, string content, string[] roles)
    {
        var chunks = DocumentChunker.Split(content);
        var embedded = new List<(Chunk, float[])>();
        foreach (var c in chunks)
            embedded.Add((c, await Embedder.EmbedAsync(c.Content)));
        await store.IndexAsync(id, title, "{}", roles, embedded, CancellationToken.None);
    }

    [SkippableFact]
    public async Task Indexa_e_busca_por_similaridade_num_pgvector_real()
    {
        Skip.If(Conn is null, "KNOWLEDGE_PG não definida — pulando teste de integração.");
        var store = await FreshStoreAsync();

        await IndexAsync(store, Guid.NewGuid(), "Manual do forno",
            "A temperatura máxima do forno é 900 graus; parada acima de 850 por mais de 5 minutos.", []);
        await IndexAsync(store, Guid.NewGuid(), "Política de férias",
            "Solicitação de férias do time comercial deve ser feita com 30 dias.", []);

        var query = await Embedder.EmbedAsync("qual a temperatura máxima do forno?");
        var hits = await store.SearchAsync(query, ["operador"], limit: 5, CancellationToken.None);

        Assert.NotEmpty(hits);
        // O doc do forno tem que vir na frente do de férias — cosseno de verdade no banco.
        Assert.Contains("forno", hits[0].Title, StringComparison.OrdinalIgnoreCase);
        Assert.True(hits[0].Score >= hits[^1].Score);
    }

    [SkippableFact]
    public async Task Filtro_de_visibilidade_por_papel_e_aplicado_na_query()
    {
        Skip.If(Conn is null, "KNOWLEDGE_PG não definida — pulando teste de integração.");
        var store = await FreshStoreAsync();

        await IndexAsync(store, Guid.NewGuid(), "Runbook restrito",
            "Procedimento de parada de emergência da linha 2.", ["admin"]);

        var query = await Embedder.EmbedAsync("parada de emergência da linha");
        var comoOperador = await store.SearchAsync(query, ["operador"], 5, CancellationToken.None);
        var comoAdmin = await store.SearchAsync(query, ["admin"], 5, CancellationToken.None);

        Assert.Empty(comoOperador);   // documento restrito a admin não sai do banco pro operador
        Assert.NotEmpty(comoAdmin);   // admin vê
    }
}
