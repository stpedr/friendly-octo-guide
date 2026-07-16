using System.Globalization;
using Mes.Connector.Domain;

namespace Mes.Connector.Worker;

/// <summary>
/// Adapter de dev: gera um evento MES a cada poll, ciclando pelos tipos, com cursor
/// monotônico (id zero-padded). Deixa o pipeline exercitável de ponta a ponta sem um
/// MES real. Em produção, troque por um adapter REST/SQL que implemente IMesAdapter.
/// </summary>
public sealed class SimulatorMesAdapter : IMesAdapter
{
    private static readonly string[] Tipos = ["Apontamento", "Defeito", "Parada", "OrdemAberta"];
    private long _seq;

    public Task<IReadOnlyList<RawMesRow>> PollAsync(string? cursor, CancellationToken cancellationToken = default)
    {
        var n = Interlocked.Increment(ref _seq);
        var tipo = Tipos[(n - 1) % Tipos.Length];

        var row = new RawMesRow(
            Cursor: n.ToString("D18", CultureInfo.InvariantCulture),
            AtivoId: "envase.spitau.linha2.enchedora",
            Tipo: tipo,
            Codigo: tipo == "Defeito" ? "SOLDER-BRIDGE" : "OK",
            Quantidade: "1",
            Texto: null,
            Turno: "dia",
            OccurredAt: DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));

        IReadOnlyList<RawMesRow> rows = [row];
        return Task.FromResult(rows);
    }
}
