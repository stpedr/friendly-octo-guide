using System.Diagnostics.CodeAnalysis;

namespace Mes.Connector.Domain;

/// <summary>Progresso do polling: o último cursor já processado. DTO puro.</summary>
[ExcludeFromCodeCoverage]
public sealed record MesPollState(string? LastCursor)
{
    public static MesPollState Start { get; } = new((string?)null);
}

/// <summary>
/// Idempotência do polling: dado um lote de linhas e o estado, devolve só as linhas
/// NOVAS (cursor &gt; último) e o próximo estado. Reprocesso (adapter reenviando o mesmo
/// lote) nunca republica. Pura. Assume cursor monotônico crescente — timestamp ISO ou id
/// zero-padded — comparado ordinalmente; o adapter é responsável por emitir cursor assim.
/// </summary>
public static class PollCursor
{
    public static (IReadOnlyList<RawMesRow> Fresh, MesPollState Next) SelectNew(
        IReadOnlyList<RawMesRow> rows, MesPollState state)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(state);

        var fresh = new List<RawMesRow>();
        var maxCursor = state.LastCursor;

        foreach (var row in rows)
        {
            if (state.LastCursor is not null && string.CompareOrdinal(row.Cursor, state.LastCursor) <= 0)
                continue;

            fresh.Add(row);
            if (maxCursor is null || string.CompareOrdinal(row.Cursor, maxCursor) > 0)
                maxCursor = row.Cursor;
        }

        return (fresh, new MesPollState(maxCursor));
    }
}
