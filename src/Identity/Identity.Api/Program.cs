using Identity.Api;
using Platform.Audit;
using Platform.ServiceDefaults;

var instrumentation = new ServiceInstrumentation("identity");

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults(instrumentation);

// OpenBao é a fonte da chave de assinatura em prod; sem ele configurado (dev local),
// cai no valor de Jwt:SigningKey do appsettings.
if (await PlatformSecrets.TryGetAsync(builder.Configuration, "platform/jwt", "signingKey") is { } jwtKey)
    builder.Configuration["Jwt:SigningKey"] = jwtKey;

// Keycloak:BaseUrl configurado → ele é a fonte da verdade de usuários/senha/TOTP;
// o client secret vem do OpenBao (nunca do compose em texto puro).
var keycloakBaseUrl = builder.Configuration["Keycloak:BaseUrl"];
KeycloakAuthClient? keycloakClient = null;
if (!string.IsNullOrEmpty(keycloakBaseUrl))
{
    var clientId = await PlatformSecrets.TryGetAsync(builder.Configuration, "platform/keycloak", "clientId") ?? "identity-api";
    var clientSecret = await PlatformSecrets.TryGetAsync(builder.Configuration, "platform/keycloak", "clientSecret");
    if (clientSecret is not null)
    {
        var realm = builder.Configuration["Keycloak:Realm"] ?? "plataforma-linha";
        keycloakClient = new KeycloakAuthClient(new HttpClient { BaseAddress = new Uri(keycloakBaseUrl) }, realm, clientId, clientSecret);
    }
}

builder.Services.AddSingleton(instrumentation);
// Uma instância do store atende leitura (IUserStore) e admin (IUserAdmin) — sem
// Keycloak (dev local), os usuários seguem em memória.
builder.Services.AddSingleton<InMemoryUserStore>();
builder.Services.AddSingleton<IUserStore>(sp => sp.GetRequiredService<InMemoryUserStore>());
builder.Services.AddSingleton<IUserAdmin>(sp => sp.GetRequiredService<InMemoryUserStore>());
builder.Services.AddSingleton<IAdminAuditTrail, LoggingAdminAuditTrail>(); // fase 0: log; fase 1: outbox → WORM
builder.Services.AddSingleton<TokenIssuer>();
if (keycloakClient is not null)
    builder.Services.AddSingleton(keycloakClient);
builder.Services.AddSingleton(sp => new LoginFlow(
    sp.GetRequiredService<IUserStore>(),
    sp.GetRequiredService<TokenIssuer>(),
    keycloakClient));

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();
app.MapAdminEndpoints();

app.Run();
