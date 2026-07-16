using System.Diagnostics.CodeAnalysis;

namespace Knowledge.Domain.Ishikawa;

/// <summary>As 6 categorias de causa do diagrama de Ishikawa (6M) + Indefinida.</summary>
public enum IshikawaCategory
{
    Metodo,
    Maquina,
    Material,
    MaoDeObra,
    Medicao,
    MeioAmbiente,
    Indefinida,
}

/// <summary>
/// Causa raiz: liga um sintoma (defeito/parada) a uma causa numa categoria 6M, com
/// confiança (0..1) pro ranking e a explicabilidade. Espelho de schemas/causa-raiz.avsc.
/// DTO puro.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RootCause(
    Guid CausaId,
    string AtivoId,
    IshikawaCategory Categoria,
    string Sintoma,
    string Causa,
    string? MotivoCodigo,
    double Confianca,
    DateTimeOffset RegistradoEm);
