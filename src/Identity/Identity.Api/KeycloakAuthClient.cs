using System.Net.Http.Json;

namespace Identity.Api;

public enum KeycloakGrantOutcome
{
    Success,
    TotpRequired,   // senha certa, mas o realm exige o segundo fator
    Denied,         // usuário inexistente, senha errada ou TOTP inválido
}

public sealed record KeycloakGrantResult(
    KeycloakGrantOutcome Outcome,
    string? AccessToken = null,
    string? RefreshToken = null);

/// <summary>
/// Fala com o token endpoint do Keycloak via Direct Access Grant (Resource Owner
/// Password Credentials) — o realm valida senha + TOTP e é quem assina o token,
/// não o Identity. Sem redirect de browser: a PWA continua batendo só no Identity,
/// que faz esta chamada de servidor para servidor.
/// </summary>
public sealed class KeycloakAuthClient(HttpClient http, string realm, string clientId, string clientSecret)
{
    public async Task<KeycloakGrantResult> PasswordGrantAsync(string username, string password, string? totp, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["username"] = username,
            ["password"] = password,
            ["scope"] = "openid",
        };
        if (totp is not null)
            form["totp"] = totp;

        return await SendAsync(form, ct);
    }

    public async Task<KeycloakGrantResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["refresh_token"] = refreshToken,
        };
        return await SendAsync(form, ct);
    }

    private async Task<KeycloakGrantResult> SendAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var response = await http.PostAsync(
            $"/realms/{realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(form), ct);

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
            return body is null
                ? new KeycloakGrantResult(KeycloakGrantOutcome.Denied)
                : new KeycloakGrantResult(KeycloakGrantOutcome.Success, body.access_token, body.refresh_token);
        }

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: ct);
        // Keycloak devolve invalid_grant tanto pra "falta TOTP" quanto pra credencial
        // errada — só a error_description distingue os dois casos.
        var needsTotp = error?.error_description?.Contains("otp", StringComparison.OrdinalIgnoreCase) == true
                      || error?.error_description?.Contains("totp", StringComparison.OrdinalIgnoreCase) == true;

        return new KeycloakGrantResult(needsTotp ? KeycloakGrantOutcome.TotpRequired : KeycloakGrantOutcome.Denied);
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private sealed class TokenResponse
    {
        public string? access_token { get; set; }
        public string? refresh_token { get; set; }
    }

    private sealed class ErrorResponse
    {
        public string? error { get; set; }
        public string? error_description { get; set; }
    }
}
