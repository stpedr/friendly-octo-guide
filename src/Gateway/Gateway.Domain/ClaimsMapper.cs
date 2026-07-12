using Platform.AccessControl;

namespace Gateway.Domain;

/// <summary>
/// Converte as claims do JWT (como o Identity as emite: "role" e "attr:*")
/// no Subject que a AccessPolicy avalia. Tupla em vez de ClaimsPrincipal
/// de propósito: testável sem montar um principal.
/// </summary>
public static class ClaimsMapper
{
    public const string AttributePrefix = "attr:";

    public static Subject ToSubject(string userId, IEnumerable<(string Type, string Value)> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (type, value) in claims)
        {
            if (string.Equals(type, "role", StringComparison.OrdinalIgnoreCase))
                roles.Add(value);
            else if (type.StartsWith(AttributePrefix, StringComparison.OrdinalIgnoreCase))
                attributes[type[AttributePrefix.Length..]] = value;
        }

        return new Subject(userId, roles, attributes);
    }
}
