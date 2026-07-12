using Identity.Domain.Tokens;

namespace Identity.Api;

public sealed record LoginRequest(string Username, string Password);
public sealed record TotpRequest(Guid ChallengeId, string Code);
public sealed record RefreshRequest(Guid RefreshToken);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/v1/auth");

        // Etapa 1: senha certa devolve só um desafio — nunca um token.
        auth.MapPost("/login", (LoginRequest req, LoginFlow flow) =>
            flow.BeginLogin(req.Username, req.Password) is { } challengeId
                ? Results.Ok(new { challengeId, next = "totp" })
                : Results.Unauthorized());

        // Etapa 2: código do authenticator fecha o desafio e emite a sessão.
        auth.MapPost("/totp", (TotpRequest req, LoginFlow flow) =>
            flow.CompleteTotp(req.ChallengeId, req.Code) is { } session
                ? Results.Ok(new { accessToken = session.AccessToken, refreshToken = session.RefreshToken })
                : Results.Unauthorized());

        auth.MapPost("/refresh", (RefreshRequest req, TokenIssuer tokens, IUserStore users) =>
        {
            var (result, username) = tokens.Redeem(req.RefreshToken);
            if (result.Outcome != RedeemOutcome.Rotated || username is null || users.Find(username) is not { } user)
                return Results.Unauthorized(); // inclui ReuseDetected: família já foi revogada lá dentro

            return Results.Ok(new
            {
                accessToken = tokens.MintAccessToken(user),
                refreshToken = result.NewToken!.Id,
            });
        });

        // Provisionamento do authenticator (dev). Em prod: exige sessão + reautenticação.
        auth.MapGet("/totp/provision/{username}", (string username, IUserStore users) =>
            users.Find(username) is { } user
                ? Results.Ok(new { otpauthUri = TotpProvisioning.OtpAuthUri(user) })
                : Results.NotFound());
    }
}
