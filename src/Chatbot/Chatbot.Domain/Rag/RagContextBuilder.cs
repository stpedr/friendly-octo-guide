using Platform.AccessControl;

namespace Chatbot.Domain.Rag;

/// <summary>Documento indexado pro RAG: manuais, histórico, telemetria sumarizada.</summary>
public sealed record RagDocument(string Id, string Content, RouteRequirement Visibility);

/// <summary>
/// Monta o contexto do RAG respeitando o RBAC do usuário logado: documento que o
/// usuário não pode ver NÃO entra no prompt — vazamento por resposta de LLM é
/// vazamento igual. Relevância lexical simples na fase 0; pgvector assume na fase 1
/// mantendo o mesmo filtro de visibilidade.
/// </summary>
public static class RagContextBuilder
{
    public static IReadOnlyList<RagDocument> Select(
        IEnumerable<RagDocument> corpus, Subject subject, string query, int maxDocuments)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(query);

        var queryTerms = Tokenize(query);

        return [.. corpus
            .Where(d => AccessPolicy.Evaluate(subject, d.Visibility) == AccessDecision.Allow)
            .Select(d => (Doc: d, Score: Tokenize(d.Content).Intersect(queryTerms).Count()))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(maxDocuments)
            .Select(x => x.Doc)];
    }

    // Stopwords não são relevância: "do", "por", "que" casam com qualquer texto em português.
    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "A", "À", "AO", "AS", "ÀS", "COM", "DA", "DAS", "DE", "DO", "DOS", "E", "EM",
        "NA", "NAS", "NO", "NOS", "O", "OS", "PARA", "PELA", "PELO", "POR", "PRA",
        "QUE", "SE", "SEM", "UM", "UMA",
    };

    private static HashSet<string> Tokenize(string text) =>
        [.. text.ToUpperInvariant()
            .Split([' ', ',', '.', ';', ':', '?', '!', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !Stopwords.Contains(t))];
}
