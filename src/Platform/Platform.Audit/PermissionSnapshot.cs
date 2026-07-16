namespace Platform.Audit;

/// <summary>
/// Foto das permissões de um usuário num instante: papéis + atributos ABAC
/// (planta, linha, turno). É o que a trilha compara pra registrar before/after
/// de uma mudança de permissão.
///
/// Classe (não record posicional) de propósito: tem lógica de verdade
/// (serialização redigida e comparação semântica) e nenhuma necessidade de
/// igualdade por valor — quem compara é <see cref="SamePermissions"/>, não ==.
/// </summary>
public sealed class PermissionSnapshot(
    IReadOnlySet<string> roles,
    IReadOnlyDictionary<string, string> attributes)
{
    public IReadOnlySet<string> Roles { get; } = roles;
    public IReadOnlyDictionary<string, string> Attributes { get; } = attributes;

    public static PermissionSnapshot Empty { get; } =
        new(new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// Serialização canônica e JÁ REDIGIDA pro campo before/after do evento:
    /// ordenada (papéis e atributos) pra que o diff seja estável e comparável,
    /// com valores sensíveis substituídos pelo placeholder.
    /// </summary>
    public string ToAuditString()
    {
        var roleList = string.Join(",", Roles.OrderBy(r => r, StringComparer.Ordinal));
        var attrList = string.Join(",", AuditRedaction.Redact(Attributes)
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}"));
        return $"roles=[{roleList}] attrs=[{attrList}]";
    }

    /// <summary>Duas fotos têm as mesmas permissões? (comparação de conjunto/mapa, não de referência)</summary>
    public bool SamePermissions(PermissionSnapshot other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!Roles.SetEquals(other.Roles) || Attributes.Count != other.Attributes.Count)
            return false;

        foreach (var (key, value) in Attributes)
        {
            if (!other.Attributes.TryGetValue(key, out var otherValue) || !string.Equals(value, otherValue, StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
