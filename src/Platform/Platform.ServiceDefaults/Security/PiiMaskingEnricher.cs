using Serilog.Core;
using Serilog.Events;

namespace Platform.ServiceDefaults.Security;

/// <summary>
/// Enricher do Serilog que mascara propriedades de PII ANTES de o log ser escrito
/// pelo sink. Nomes conhecidos (email, senha, cpf, telefone, seed TOTP…) nunca
/// saem em claro, independentemente de quem logou — a proteção é da espinha, não
/// da disciplina de cada call-site. E-mails preservam o domínio; o resto é
/// mascarado por completo.
/// </summary>
public sealed class PiiMaskingEnricher(IReadOnlySet<string> emailProperties, IReadOnlySet<string> maskedProperties)
    : ILogEventEnricher
{
    /// <summary>Conjunto padrão de nomes de propriedade tratados como PII no monorepo.</summary>
    public static PiiMaskingEnricher Default() => new(
        emailProperties: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "email", "Email", "userEmail" },
        maskedProperties: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password", "senha", "cpf", "documento", "telefone", "phone",
            "totp", "totpSeed", "seed", "refreshToken", "accessToken", "clientSecret",
        });

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        foreach (var (name, value) in logEvent.Properties)
        {
            if (value is not ScalarValue { Value: string raw })
                continue;

            string? masked = null;
            if (emailProperties.Contains(name))
                masked = PiiMasker.MaskEmail(raw);
            else if (maskedProperties.Contains(name))
                masked = PiiMasker.Mask(raw);

            if (masked is not null)
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(name, masked));
        }
    }
}
