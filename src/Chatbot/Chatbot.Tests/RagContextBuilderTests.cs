using Chatbot.Domain.Rag;
using Platform.AccessControl;
using Xunit;

namespace Chatbot.Tests;

public class RagContextBuilderTests
{
    private static readonly RouteRequirement QualquerOperador = RouteRequirement.ForRoles("operador", "admin");
    private static readonly RouteRequirement SoAdmin = RouteRequirement.ForRoles("admin");

    private static readonly RagDocument ManualForno = new(
        "manual-forno", "Manual do forno: temperatura de operação e limites da linha", QualquerOperador);
    private static readonly RagDocument HistoricoParadas = new(
        "historico", "Histórico de paradas da linha 2 por temperatura alta", QualquerOperador);
    private static readonly RagDocument RelatorioDiretoria = new(
        "diretoria", "Relatório financeiro da linha e custos por parada", SoAdmin);

    private static Subject Operador() => new(
        "maria", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "operador" },
        new Dictionary<string, string>());

    [Fact]
    public void Seleciona_por_relevancia_lexical()
    {
        var docs = RagContextBuilder.Select(
            [ManualForno, HistoricoParadas], Operador(),
            query: "por que a linha 2 parou por temperatura?", maxDocuments: 1);

        Assert.Equal(["historico"], docs.Select(d => d.Id));
    }

    [Fact]
    public void Documento_fora_do_rbac_do_usuario_nunca_entra_no_prompt()
    {
        var docs = RagContextBuilder.Select(
            [ManualForno, RelatorioDiretoria], Operador(),
            query: "custos da linha por parada", maxDocuments: 5);

        Assert.DoesNotContain(docs, d => d.Id == "diretoria");
    }

    [Fact]
    public void Sem_sobreposicao_de_termos_nao_ha_contexto()
    {
        var docs = RagContextBuilder.Select(
            [ManualForno], Operador(), query: "férias do refeitório", maxDocuments: 5);

        Assert.Empty(docs); // contexto irrelevante induz alucinação — melhor nenhum
    }

    [Fact]
    public void Respeita_o_orcamento_de_documentos()
    {
        var docs = RagContextBuilder.Select(
            [ManualForno, HistoricoParadas], Operador(), query: "linha temperatura", maxDocuments: 1);

        Assert.Single(docs);
    }
}
