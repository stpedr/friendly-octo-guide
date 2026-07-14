using Identity.Api;
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
builder.Services.AddSingleton<IUserStore, InMemoryUserStore>(); // sem Keycloak (dev local): usuários seguem em memória
builder.Services.AddSingleton<TokenIssuer>();
builder.Services.AddSingleton(keycloakClient!); // pode ser null — LoginFlow/AuthEndpoints tratam os dois casos
builder.Services.AddSingleton<LoginFlow>();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();

app.Run();
