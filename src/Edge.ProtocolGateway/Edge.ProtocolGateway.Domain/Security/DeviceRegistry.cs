namespace Edge.ProtocolGateway.Domain.Security;

public enum DeviceTrust
{
    Trusted,      // cert enrolado, válido e não revogado — pode publicar
    Unknown,      // thumbprint não enrolado — device desconhecido, recusa
    Revoked,      // baixa/comprometimento — recusa mesmo dentro da validade
    Expired,      // passou de NotAfter
    NotYetValid,  // antes de NotBefore (relógio do device adiantado?)
}

/// <summary>Certificado X.509 individual de um sensor/PLC — a identidade mTLS por dispositivo.</summary>
public sealed record DeviceCertificate(
    string DeviceId, string Thumbprint, DateTimeOffset NotBefore, DateTimeOffset NotAfter);

/// <summary>
/// Registro de identidade da borda: cada sensor/PLC tem um cert X.509 próprio, então
/// um device comprometido não envenena o pipeline — ele é revogado individualmente,
/// sem derrubar a linha. Cobre o ciclo de vida: enrollment (device novo), revogação
/// (baixa/comprometimento, uma CRL local) e validação por mensagem. Determinístico.
/// </summary>
public sealed class DeviceRegistry
{
    private readonly Dictionary<string, DeviceCertificate> _enrolled = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _revoked = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Enrolla um device novo. Reenrolar (troca de cert) substitui o anterior.</summary>
    public void Enroll(DeviceCertificate cert)
    {
        ArgumentNullException.ThrowIfNull(cert);
        _enrolled[cert.Thumbprint] = cert;
        _revoked.Remove(cert.Thumbprint); // cert novo com o mesmo thumbprint (raro) volta a valer
    }

    /// <summary>Revoga um cert pelo thumbprint (CRL). Idempotente.</summary>
    public void Revoke(string thumbprint) => _revoked.Add(thumbprint);

    /// <summary>É a decisão que a borda toma ANTES de aceitar um byte do device.</summary>
    public DeviceTrust Evaluate(string thumbprint, DateTimeOffset now)
    {
        if (!_enrolled.TryGetValue(thumbprint, out var cert))
            return DeviceTrust.Unknown;
        if (_revoked.Contains(thumbprint))
            return DeviceTrust.Revoked;
        if (now < cert.NotBefore)
            return DeviceTrust.NotYetValid;
        if (now >= cert.NotAfter)
            return DeviceTrust.Expired;
        return DeviceTrust.Trusted;
    }

    /// <summary>Atalho: só device confiável publica.</summary>
    public bool IsAllowed(string thumbprint, DateTimeOffset now) =>
        Evaluate(thumbprint, now) == DeviceTrust.Trusted;

    /// <summary>Certs que expiram dentro da janela — o enrollment agenda a rotação antes de virar Expired.</summary>
    public IReadOnlyList<DeviceCertificate> ExpiringWithin(TimeSpan window, DateTimeOffset now) =>
        [.. _enrolled.Values
            .Where(c => !_revoked.Contains(c.Thumbprint) && c.NotAfter > now && c.NotAfter - now <= window)
            .OrderBy(c => c.NotAfter)];
}
