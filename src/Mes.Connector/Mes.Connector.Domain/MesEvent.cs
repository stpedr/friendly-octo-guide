using System.Diagnostics.CodeAnalysis;

namespace Mes.Connector.Domain;

/// <summary>
/// Tipo do evento MES. Mesma ordem do enum do contrato (Platform.Contracts.TipoMesEvento)
/// — o Worker mapeia domínio→contrato por valor.
/// </summary>
public enum MesEventType { OrdemAberta, OrdemFechada, Apontamento, Defeito, Parada, Setup }

/// <summary>Evento MES normalizado (domínio). DTO puro.</summary>
[ExcludeFromCodeCoverage]
public sealed record MesEvent(
    Guid EventId,
    string AtivoId,
    MesEventType Tipo,
    string Codigo,
    double? Quantidade,
    string? Texto,
    string? Turno,
    string SistemaOrigem,
    DateTimeOffset OccurredAt);

/// <summary>
/// Linha crua vinda do adapter MES (REST/SQL/simulador): tudo string, como chega da
/// fonte. O normalizador valida e converte. <see cref="Cursor"/> é a chave monotônica
/// de progresso (timestamp ISO ou id zero-padded) usada pela idempotência do polling.
/// DTO puro.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RawMesRow(
    string Cursor,
    string AtivoId,
    string Tipo,
    string Codigo,
    string? Quantidade = null,
    string? Texto = null,
    string? Turno = null,
    string? OccurredAt = null);
