namespace Gateway.Domain;

/// <summary>Cabeçalhos de depreciação de uma rota (RFC 8594): Deprecation + Sunset + Link.</summary>
public sealed record DeprecationHeaders(string Deprecation, string Sunset, string? Successor);

/// <summary>
/// Política de deprecação de API versionada na borda: uma rota antiga (ex.: /v1/…)
/// anuncia sua aposentadoria ANTES de sumir — cliente recebe Deprecation: true e
/// Sunset com a data, via RFC 8594, e um Link pro sucessor (/v2/…). O contrato
/// muda com aviso, não de surpresa. Prefixo mais longo vence.
/// </summary>
public sealed class DeprecationTable
{
    private readonly List<(string Prefix, DateTimeOffset Sunset, string? Successor)> _routes = [];

    public DeprecationTable Deprecate(string prefix, DateTimeOffset sunset, string? successor = null)
    {
        _routes.Add(("/" + prefix.Trim('/'), sunset, successor));
        return this;
    }

    /// <summary>Cabeçalhos a adicionar pra este caminho, ou null se a rota não está deprecada.</summary>
    public DeprecationHeaders? For(string path)
    {
        var normalized = "/" + path.Trim('/');
        var match = _routes
            .Where(r => normalized.StartsWith(r.Prefix, StringComparison.OrdinalIgnoreCase)
                     && (normalized.Length == r.Prefix.Length || normalized[r.Prefix.Length] == '/'))
            .OrderByDescending(r => r.Prefix.Length)
            .Select(r => (r.Sunset, r.Successor))
            .FirstOrDefault();

        return match.Sunset == default
            ? null
            : new DeprecationHeaders(
                Deprecation: "true",
                Sunset: match.Sunset.ToUniversalTime().ToString("R"), // HTTP-date, como manda a RFC
                Successor: match.Successor);
    }
}
