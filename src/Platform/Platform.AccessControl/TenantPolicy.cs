namespace Platform.AccessControl;

public enum TenantDecision { Allow, DenyCrossTenant, DenyNoTenant }

/// <summary>
/// Isolamento multi-tenant sobre o MESMO mecanismo de ABAC: o tenant é um atributo
/// do subject (claim "tenant"), e um recurso de outro tenant é negado. A decisão de
/// arquitetura (docs/governanca/multi-tenant.md) é single-tenant por instância —
/// então isto vem DESLIGADO por padrão (<paramref name="enabled"/> = false, tudo
/// passa). Ligar é o plumbing pronto pro dia em que uma instância hospedar vários
/// tenants; sem ligar, não custa nada. Determinístico.
/// </summary>
public static class TenantPolicy
{
    public const string TenantAttribute = "tenant";

    public static TenantDecision Evaluate(Subject subject, string resourceTenant, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentException.ThrowIfNullOrEmpty(resourceTenant);

        if (!enabled)
            return TenantDecision.Allow; // single-tenant: o tenant do recurso é irrelevante

        if (!subject.Attributes.TryGetValue(TenantAttribute, out var subjectTenant) || string.IsNullOrEmpty(subjectTenant))
            return TenantDecision.DenyNoTenant; // multi-tenant ligado exige o claim

        return string.Equals(subjectTenant, resourceTenant, StringComparison.OrdinalIgnoreCase)
            ? TenantDecision.Allow
            : TenantDecision.DenyCrossTenant; // tenant A jamais toca recurso do tenant B
    }
}
