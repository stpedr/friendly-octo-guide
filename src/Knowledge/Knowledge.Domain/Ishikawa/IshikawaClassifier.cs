namespace Knowledge.Domain.Ishikawa;

/// <summary>
/// Classifica um sintoma (código de defeito, motivo de parada, texto) numa categoria
/// 6M por palavra-chave. É a semente do sistema especialista (Épico 5): regras
/// interpretáveis e explicáveis (XAI), não caixa-preta. A base cresce com a elicitação
/// da operação. Pura e determinística.
/// </summary>
public static class IshikawaClassifier
{
    /// <summary>Regras semente com vocabulário de linha SMT/SMD. Palavra (minúscula) → categoria.</summary>
    public static IReadOnlyList<(string Palavra, IshikawaCategory Categoria)> RegrasPadrao { get; } =
    [
        ("estencil", IshikawaCategory.Maquina),
        ("estêncil", IshikawaCategory.Maquina),
        ("desgaste", IshikawaCategory.Maquina),
        ("squeegee", IshikawaCategory.Maquina),
        ("nozzle", IshikawaCategory.Maquina),
        ("pick", IshikawaCategory.Maquina),
        ("pasta", IshikawaCategory.Material),
        ("solda", IshikawaCategory.Material),
        ("componente", IshikawaCategory.Material),
        ("umidade", IshikawaCategory.MeioAmbiente),
        ("temperatura", IshikawaCategory.MeioAmbiente),
        ("operador", IshikawaCategory.MaoDeObra),
        ("turno", IshikawaCategory.MaoDeObra),
        ("treinamento", IshikawaCategory.MaoDeObra),
        ("spi", IshikawaCategory.Medicao),
        ("aoi", IshikawaCategory.Medicao),
        ("calibr", IshikawaCategory.Medicao),
        ("medicao", IshikawaCategory.Medicao),
        ("setup", IshikawaCategory.Metodo),
        ("procedimento", IshikawaCategory.Metodo),
        ("receita", IshikawaCategory.Metodo),
    ];

    /// <summary>Classifica com as regras semente.</summary>
    public static IshikawaCategory Classify(string sintomaOuCodigo) =>
        Classify(sintomaOuCodigo, RegrasPadrao);

    /// <summary>Classifica com um conjunto de regras (a ordem decide o empate: primeira que casa vence).</summary>
    public static IshikawaCategory Classify(
        string sintomaOuCodigo, IReadOnlyList<(string Palavra, IshikawaCategory Categoria)> regras)
    {
        ArgumentNullException.ThrowIfNull(sintomaOuCodigo);
        ArgumentNullException.ThrowIfNull(regras);

        var texto = sintomaOuCodigo.ToLowerInvariant();
        foreach (var (palavra, categoria) in regras)
        {
            if (texto.Contains(palavra, StringComparison.Ordinal))
                return categoria;
        }

        return IshikawaCategory.Indefinida;
    }
}
