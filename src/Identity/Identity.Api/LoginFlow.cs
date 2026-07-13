using System.Collections.Concurrent;
using Identity.Domain.Totp;

namespace Identity.Api;

/// <summary>
/// Login em duas etapas: senha correta abre um desafio TOTP de vida curta (2 min);
/// só o código do authenticator fecha o desafio e emite sessão. A senha certa
/// sozinha NUNCA emite token — é o que o RFC 6238 está fazendo aqui.
///
/// Com Keycloak configurado, ele é quem valida senha+TOTP e assina o token — o
/// Identity só guarda a senha em texto plano pelo tempo do desafio (2 min, uso
/// único) porque o grant do Keycloak precisa dela de novo na etapa 2. Sem
/// Keycloak (dev local), cai no fluxo 100% local de sempre.
/// </summary>
public sealed class LoginFlow(IUserStore users, TokenIssuer tokens, KeycloakAuthClient? keycloak)
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(2);
    private readonly ConcurrentDictionary<Guid, (string Username, string Password, DateTimeOffset ExpiresAt)> _challenges = new();

    public async Task<Guid?> BeginLogin(string username, string password)
    {
        if (keycloak is not null)
        {
            var attempt = await keycloak.PasswordGrantAsync(username, password, totp: null);
            if (attempt.Outcome != KeycloakGrantOutcome.TotpRequired)
                return null; // credencial errada, OU logou sem TOTP — usuário deveria ter 2FA configurado

            var challengeId = Guid.NewGuid();
            _challenges[challengeId] = (username, password, DateTimeOffset.UtcNow + ChallengeLifetime);
            return challengeId;
        }

        var user = users.Find(username);
        if (user is null || !PasswordHasher.Verify(password, user.PasswordSalt, user.PasswordHash))
            return null; // mesma resposta pra usuário inexistente e senha errada — não vaza enumeração

        var localChallengeId = Guid.NewGuid();
        _challenges[localChallengeId] = (user.Username, string.Empty, DateTimeOffset.UtcNow + ChallengeLifetime);
        return localChallengeId;
    }

    public async Task<(string AccessToken, string RefreshToken)?> CompleteTotp(Guid challengeId, string code)
    {
        if (!_challenges.TryRemove(challengeId, out var challenge) || DateTimeOffset.UtcNow >= challenge.ExpiresAt)
            return null;

        if (keycloak is not null)
        {
            var result = await keycloak.PasswordGrantAsync(challenge.Username, challenge.Password, code);
            return result is { Outcome: KeycloakGrantOutcome.Success, AccessToken: { } at, RefreshToken: { } rt }
                ? (at, rt)
                : null;
        }

        var user = users.Find(challenge.Username);
        if (user is null)
            return null;

        var (result2, acceptedStep) = TotpValidator.Validate(
            user.TotpSeed, code, DateTimeOffset.UtcNow, user.LastAcceptedTotpStep);
        if (result2 != TotpResult.Valid)
            return null;

        user.LastAcceptedTotpStep = acceptedStep;
        var (accessToken, refreshToken) = tokens.IssueFor(user);
        return (accessToken, refreshToken.ToString());
    }
}
