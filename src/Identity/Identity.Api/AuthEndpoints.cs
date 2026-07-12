using Identity.Domain.Tokens;

namespace Identity.Api;

public sealed record LoginRequest(string Username, string Password);
public sealed record TotpRequest(Guid ChallengeId, string Code);
public sealed record RefreshRequest(string RefreshToken);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/v1/auth");

        // Etapa 1: senha certa devolve só um desafio — nunca um token.
        auth.MapPost("/login", async (LoginRequest req, LoginFlow flow) =>
            await flow.BeginLogin(req.Username, req.Password) is { } challengeId
                ? Results.Ok(new { challengeId, next = "totp" })
                : Results.Unauthorized());

        // Etapa 2: código do authenticator fecha o desafio e emite a sessão.
        auth.MapPost("/totp", async (TotpRequest req, LoginFlow flow) =>
            await flow.CompleteTotp(req.ChallengeId, req.Code) is { } session
                ? Results.Ok(new { accessToken = session.AccessToken, refreshToken = session.RefreshToken })
                : Results.Unauthorized());

        auth.MapPost("/refresh", async (RefreshRequest req, TokenIssuer tokens, IUserStore users, KeycloakAuthClient? keycloak) =>
        {
            if (keycloak is not null)
            {
                var result = await keycloak.RefreshAsync(req.RefreshToken);
                return result is { Outcome: KeycloakGrantOutcome.Success, AccessToken: { } at, RefreshToken: { } rt }
                    ? Results.Ok(new { accessToken = at, refreshToken = rt })
                    : Results.Unauthorized();
            }

            if (!Guid.TryParse(req.RefreshToken, out var refreshTokenId))
                return Results.Unauthorized();

            var (redeemResult, username) = tokens.Redeem(refreshTokenId);
            if (redeemResult.Outcome != RedeemOutcome.Rotated || username is null || users.Find(username) is not { } user)
                return Results.Unauthorized(); // inclui ReuseDetected: família já foi revogada lá dentro

            return Results.Ok(new
            {
                accessToken = tokens.MintAccessToken(user),
                refreshToken = redeemResult.NewToken!.Id.ToString(),
            });
        });

        // Provisionamento do authenticator (dev, sem Keycloak). Em prod: o QR é o do
        // Keycloak (Account Console), o Identity não guarda mais a seed TOTP.
        auth.MapGet("/totp/provision/{username}", (string username, IUserStore users, KeycloakAuthClient? keycloak) =>
            keycloak is not null
                ? Results.Conflict(new { message = "Com Keycloak, o authenticator é configurado no Account Console dele." })
                : users.Find(username) is { } user
                    ? Results.Ok(new { otpauthUri = TotpProvisioning.OtpAuthUri(user) })
                    : Results.NotFound());
    }
}
