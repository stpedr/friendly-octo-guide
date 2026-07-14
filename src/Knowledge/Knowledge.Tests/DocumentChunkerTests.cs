using Knowledge.Domain.Chunking;
using Xunit;

namespace Knowledge.Tests;

public class DocumentChunkerTests
{
    [Fact]
    public void Documento_curto_vira_um_chunk_so()
    {
        var chunks = DocumentChunker.Split("Manual da prensa.\n\nAperte o botão verde.");

        var chunk = Assert.Single(chunks);
        Assert.Equal(0, chunk.Sequence);
        Assert.Contains("botão verde", chunk.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Paragrafos_sao_agrupados_ate_o_teto_sem_quebrar_no_meio()
    {
        var p1 = new string('a', 500);
        var p2 = new string('b', 500);
        var p3 = new string('c', 500);

        var chunks = DocumentChunker.Split($"{p1}\n\n{p2}\n\n{p3}", maxChars: 1100);

        Assert.Equal(2, chunks.Count);
        Assert.Contains(p1, chunks[0].Content, StringComparison.Ordinal);
        Assert.Contains(p2, chunks[0].Content, StringComparison.Ordinal);
        Assert.Contains(p3, chunks[1].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Paragrafo_gigante_e_fatiado_com_sobreposicao()
    {
        var giant = new string('x', 300);

        var chunks = DocumentChunker.Split(giant, maxChars: 100, overlapChars: 20);

        Assert.True(chunks.Count >= 3);
        // Nada se perde: juntando os pedaços (descontada a sobreposição) volta o texto.
        Assert.All(chunks, c => Assert.True(c.Content.Length <= 100));
        Assert.Equal(giant.Length, chunks[0].Content.Length
            + chunks.Skip(1).Sum(c => c.Content.Length - 20)
            + (chunks[^1].Content.Length <= 20 ? 20 - chunks[^1].Content.Length : 0));
    }

    [Fact]
    public void Sequencia_e_continua_e_comeca_em_zero()
    {
        var text = string.Join("\n\n", Enumerable.Range(0, 10).Select(i => new string((char)('a' + i), 400)));

        var chunks = DocumentChunker.Split(text, maxChars: 500);

        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(c => c.Sequence));
    }

    [Fact]
    public void Mesmo_texto_produz_os_mesmos_chunks()
    {
        const string text = "Procedimento de parada.\n\nDesligue a esteira.\n\nTrave a chave geral.";
        Assert.Equal(DocumentChunker.Split(text), DocumentChunker.Split(text));
    }
}
