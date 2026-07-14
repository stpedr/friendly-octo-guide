using Knowledge.Domain.Embeddings;
using Xunit;

namespace Knowledge.Tests;

public class HashingEmbedderTests
{
    private static readonly HashingEmbedder Embedder = new(dimensions: 64);

    [Fact]
    public async Task Mesmo_texto_gera_o_mesmo_vetor()
    {
        var a = await Embedder.EmbedAsync("temperatura alta na prensa 3");
        var b = await Embedder.EmbedAsync("temperatura alta na prensa 3");
        Assert.Equal(a, b);
    }

    [Fact]
    public async Task Vetor_e_normalizado_em_L2()
    {
        var v = await Embedder.EmbedAsync("vibração acima do limite no motor");
        var norm = MathF.Sqrt(v.Sum(x => x * x));
        Assert.Equal(1f, norm, precision: 3);
    }

    [Fact]
    public async Task Textos_com_vocabulario_comum_ficam_mais_proximos_que_textos_disjuntos()
    {
        var query = await Embedder.EmbedAsync("temperatura da prensa");
        var relevant = await Embedder.EmbedAsync("a temperatura máxima da prensa é 80 graus");
        var unrelated = await Embedder.EmbedAsync("cronograma de férias do time comercial");

        Assert.True(Cosine(query, relevant) > Cosine(query, unrelated));
    }

    [Fact]
    public async Task Texto_vazio_gera_vetor_nulo_sem_explodir()
    {
        var v = await Embedder.EmbedAsync("");
        Assert.All(v, x => Assert.Equal(0f, x));
    }

    private static float Cosine(float[] a, float[] b) =>
        a.Zip(b, (x, y) => x * y).Sum(); // ambos já normalizados
}
