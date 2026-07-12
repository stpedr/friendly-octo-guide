namespace Identity.Domain.Tokens;

public sealed record RefreshToken(Guid Id, Guid FamilyId, DateTimeOffset ExpiresAt, bool Used);

public enum RedeemOutcome
{
    Rotated,        // token válido → novo token emitido, antigo marcado como usado
    Expired,
    ReuseDetected,  // token JÁ usado foi apresentado → família inteira revogada (roubo provável)
    Unknown,
}

public sealed record RedeemResult(RedeemOutcome Outcome, RefreshToken? NewToken, bool RevokeFamily);

/// <summary>
/// Rotação de refresh token com detecção de roubo (RFC 6819 §5.2.2.3):
/// cada token é de uso único; reapresentar um token consumido prova que duas partes
/// têm a mesma credencial — a família inteira é revogada e o usuário reloga.
/// </summary>
public sealed class RefreshTokenFamily(TimeSpan tokenLifetime)
{
    private readonly Dictionary<Guid, RefreshToken> _tokens = [];

    public RefreshToken Issue(Guid familyId, DateTimeOffset now)
    {
        var token = new RefreshToken(Guid.NewGuid(), familyId, now + tokenLifetime, Used: false);
        _tokens[token.Id] = token;
        return token;
    }

    public RedeemResult Redeem(Guid tokenId, DateTimeOffset now)
    {
        if (!_tokens.TryGetValue(tokenId, out var token))
            return new(RedeemOutcome.Unknown, null, RevokeFamily: false);

        if (token.Used)
        {
            // Reuso = comprometimento: revoga todos os tokens da família.
            foreach (var id in _tokens.Where(t => t.Value.FamilyId == token.FamilyId).Select(t => t.Key).ToList())
                _tokens.Remove(id);
            return new(RedeemOutcome.ReuseDetected, null, RevokeFamily: true);
        }

        if (now >= token.ExpiresAt)
            return new(RedeemOutcome.Expired, null, RevokeFamily: false);

        _tokens[tokenId] = token with { Used = true };
        return new(RedeemOutcome.Rotated, Issue(token.FamilyId, now), RevokeFamily: false);
    }
}
