namespace Mes.Connector.Domain;

/// <summary>
/// Porta para o MES concreto. A fase 0 usa um simulador; um <c>RestMesAdapter</c> ou
/// <c>SqlMesAdapter</c> pluga aqui quando o MES real existir — sem tocar no domínio nem
/// no Worker. Devolve as linhas cruas a partir do cursor informado (exclusive).
/// </summary>
public interface IMesAdapter
{
    Task<IReadOnlyList<RawMesRow>> PollAsync(string? cursor, CancellationToken cancellationToken = default);
}
