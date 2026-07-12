using System.Collections.Concurrent;
using Identity.Domain.Totp;

namespace Identity.Api;

/// <summary>
/// Login em duas etapas: senha correta abre um desafio TOTP de vida curta (2 min);
/// só o código do authenticator fecha o desafio e emite sessão. A senha certa
/// sozinha NUNCA emite token — é o que o RFC 6238 está fazendo aqui.
/// </summary>
public sealed class LoginFlow(IUserStore users, TokenIssuer tokens)
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(2);
    private readonly ConcurrentDictionary<Guid, (string Username, DateTimeOffset ExpiresAt)> _challenges = new();

    public Guid? BeginLogin(string username, string password)
    {
        var user = users.Find(username);
        if (user is null || !PasswordHasher.Verify(password, user.PasswordSalt, user.PasswordHash))
            return null; // mesma resposta pra usuário inexistente e senha errada — não vaza enumeração

        var challengeId = Guid.NewGuid();
        _challenges[challengeId] = (user.Username, DateTimeOffset.UtcNow + ChallengeLifetime);
        return challengeId;
    }

    public (string AccessToken, Guid RefreshToken)? CompleteTotp(Guid challengeId, string code)
    {
        if (!_challenges.TryRemove(challengeId, out var challenge) || DateTimeOffset.UtcNow >= challenge.ExpiresAt)
            return null;

        var user = users.Find(challenge.Username);
        if (user is null)
            return null;

        var (result, acceptedStep) = TotpValidator.Validate(
            user.TotpSeed, code, DateTimeOffset.UtcNow, user.LastAcceptedTotpStep);
        if (result != TotpResult.Valid)
            return null;

        user.LastAcceptedTotpStep = acceptedStep;
        return tokens.IssueFor(user);
    }
}
