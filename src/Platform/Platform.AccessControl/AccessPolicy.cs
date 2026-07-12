namespace Platform.AccessControl;

/// <summary>Quem pede: papel + atributos (planta, linha, turno) — pronto pra multi-planta.</summary>
public sealed record Subject(
    string UserId,
    IReadOnlySet<string> Roles,
    IReadOnlyDictionary<string, string> Attributes);

/// <summary>
/// O que a rota exige: ao menos um dos papéis E todos os atributos listados.
/// Atributo exigido com valor "*" significa "precisa ter o atributo, qualquer valor".
/// </summary>
public sealed record RouteRequirement(
    IReadOnlySet<string> AnyOfRoles,
    IReadOnlyDictionary<string, string> RequiredAttributes)
{
    public static RouteRequirement ForRoles(params string[] roles) =>
        new(roles.ToHashSet(StringComparer.OrdinalIgnoreCase), new Dictionary<string, string>());
}

public enum AccessDecision { Allow, DenyMissingRole, DenyMissingAttribute }

/// <summary>
/// RBAC decide "quem pode", ABAC restringe "onde/quando": papel dá a capacidade,
/// atributos (planta, linha, turno) limitam o escopo. Negação carrega o motivo —
/// vira log estruturado no Gateway, nunca um 403 mudo.
/// </summary>
public static class AccessPolicy
{
    public static AccessDecision Evaluate(Subject subject, RouteRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(requirement);

        if (requirement.AnyOfRoles.Count > 0 && !requirement.AnyOfRoles.Overlaps(subject.Roles))
            return AccessDecision.DenyMissingRole;

        foreach (var (key, expected) in requirement.RequiredAttributes)
        {
            if (!subject.Attributes.TryGetValue(key, out var actual))
                return AccessDecision.DenyMissingAttribute;
            if (expected != "*" && !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                return AccessDecision.DenyMissingAttribute;
        }

        return AccessDecision.Allow;
    }
}
