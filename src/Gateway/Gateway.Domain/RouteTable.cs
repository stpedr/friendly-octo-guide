using Platform.AccessControl;

namespace Gateway.Domain;

/// <summary>
/// Mapa rota → exigência de acesso, avaliado ANTES do proxy encaminhar qualquer byte.
/// Prefixo mais longo vence (rota mais específica manda). Rota sem entrada é negada
/// por padrão — só o que está listado como público passa sem token.
/// </summary>
public sealed class RouteTable
{
    private readonly List<(string Prefix, RouteRequirement? Requirement)> _routes = [];

    /// <summary>Rota pública: não exige token (ex.: /v1/auth/login).</summary>
    public RouteTable Public(string prefix)
    {
        _routes.Add((Normalize(prefix), null));
        return this;
    }

    public RouteTable Require(string prefix, RouteRequirement requirement)
    {
        _routes.Add((Normalize(prefix), requirement));
        return this;
    }

    public RouteMatch Match(string path)
    {
        var normalized = Normalize(path);
        var best = _routes
            .Where(r => normalized.StartsWith(r.Prefix, StringComparison.OrdinalIgnoreCase)
                     && (normalized.Length == r.Prefix.Length || normalized[r.Prefix.Length] == '/'))
            .OrderByDescending(r => r.Prefix.Length)
            .ToList();

        if (best.Count == 0)
            return RouteMatch.Unlisted;

        var (_, requirement) = best[0];
        return requirement is null ? RouteMatch.PublicRoute : RouteMatch.Protected(requirement);
    }

    private static string Normalize(string path) => "/" + path.Trim('/');
}

public sealed record RouteMatch(bool IsListed, bool IsPublic, RouteRequirement? Requirement)
{
    public static readonly RouteMatch Unlisted = new(false, false, null);
    public static readonly RouteMatch PublicRoute = new(true, true, null);
    public static RouteMatch Protected(RouteRequirement requirement) => new(true, false, requirement);
}
