using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Mes.Connector.Domain;

/// <summary>Resultado da normalização: aceito (com evento) ou rejeitado (com motivo). DTO puro.</summary>
[ExcludeFromCodeCoverage]
public sealed record NormalizeResult(bool Accepted, MesEvent? Event, string Reason)
{
    public static NormalizeResult Ok(MesEvent evento) => new(true, evento, "");
    public static NormalizeResult Rejected(string reason) => new(false, null, reason);
}

/// <summary>
/// Converte a linha crua do adapter num <see cref="MesEvent"/> validado — o "quality gate"
/// do MES. Pura e determinística: o id novo entra por função (testável). Campo obrigatório
/// ausente ou valor malformado NÃO vira evento: vira rejeição com motivo (o Worker manda
/// pra quarentena — nada se perde calado).
/// </summary>
public static class MesNormalizer
{
    public static NormalizeResult Normalize(RawMesRow row, string sistemaOrigem, Func<Guid> newId)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentException.ThrowIfNullOrWhiteSpace(sistemaOrigem);
        ArgumentNullException.ThrowIfNull(newId);

        if (string.IsNullOrWhiteSpace(row.AtivoId))
            return NormalizeResult.Rejected("AtivoAusente");
        if (string.IsNullOrWhiteSpace(row.Codigo))
            return NormalizeResult.Rejected("CodigoAusente");
        if (!Enum.TryParse<MesEventType>(row.Tipo, ignoreCase: true, out var tipo) || !Enum.IsDefined(tipo))
            return NormalizeResult.Rejected("TipoInvalido");

        double? quantidade = null;
        if (!string.IsNullOrWhiteSpace(row.Quantidade))
        {
            if (!double.TryParse(row.Quantidade, NumberStyles.Float, CultureInfo.InvariantCulture, out var q))
                return NormalizeResult.Rejected("QuantidadeInvalida");
            quantidade = q;
        }

        var occurredAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(row.OccurredAt))
        {
            if (!DateTimeOffset.TryParse(row.OccurredAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var ts))
                return NormalizeResult.Rejected("TimestampInvalido");
            occurredAt = ts;
        }

        return NormalizeResult.Ok(new MesEvent(
            newId(), row.AtivoId.Trim(), tipo, row.Codigo.Trim(),
            quantidade, NullIfBlank(row.Texto), NullIfBlank(row.Turno), sistemaOrigem, occurredAt));
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
