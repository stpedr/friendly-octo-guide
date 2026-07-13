using System.Collections.Concurrent;
using System.Security.Claims;
using Gateway.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Platform.AccessControl;
using Platform.ServiceDefaults;

// Ponto único de entrada: valida o JWT emitido pelo Identity (ou, quando o Keycloak
// está configurado, o JWT que o Identity repassa do Keycloak), aplica RBAC/ABAC por
// rota, limita taxa por usuário e propaga o traceparent (W3C) — o trace-id raiz nasce aqui.

var instrumentation = new ServiceInstrumentation("gateway");

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults(instrumentation);

if (await PlatformSecrets.TryGetAsync(builder.Configuration, "platform/jwt", "signingKey") is { } jwtKey)
    builder.Configuration["Jwt:SigningKey"] = jwtKey;

// Keycloak configurado (Keycloak:BaseUrl) → valida via JWKS do realm (assinatura assimétrica,
// Keycloak é a fonte da verdade). Sem ele → chave simétrica compartilhada com o Identity local.
var keycloakBaseUrl = builder.Configuration["Keycloak:BaseUrl"];
var keycloakRealm = builder.Configuration["Keycloak:Realm"] ?? "plataforma-linha";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        if (!string.IsNullOrEmpty(keycloakBaseUrl))
        {
            o.Authority = $"{keycloakBaseUrl}/realms/{keycloakRealm}";
            o.RequireHttpsMetadata = false; // rede interna do compose/cluster, sem TLS entre serviços ainda
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidAudience = "plataforma-linha",
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        }
        else
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = "identity",
                ValidAudience = "plataforma-linha",
                IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:SigningKey"] ?? "dev-only-signing-key-with-32-bytes!!")),
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        }
    });
builder.Services.AddAuthorization();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// A tabela de rotas é a política de acesso da borda — versionada junto com o código.
builder.Services.AddSingleton(new RouteTable()
    .Public("/v1/auth")
    .Public("/healthz")
    .Require("/v1/core", RouteRequirement.ForRoles("operador", "admin"))
    .Require("/v1/core/admin", RouteRequirement.ForRoles("admin"))
    .Require("/v1/chat", RouteRequirement.ForRoles("operador", "admin"))
    .Require("/v1/linha", new RouteRequirement(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "operador", "admin" },
        new Dictionary<string, string>())));

var app = builder.Build();

app.UseAuthentication();

// Rate limit por usuário autenticado (ou IP, pré-login): 20 req de burst, 10 req/s de regime.
// Estado local por réplica na fase 0; fase 1 move pro Valkey pra valer no agregado.
var buckets = new ConcurrentDictionary<string, TokenBucket>();
app.Use(async (ctx, next) =>
{
    var key = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? ctx.User.FindFirstValue("sub")
           ?? ctx.Connection.RemoteIpAddress?.ToString()
           ?? "anonymous";
    var bucket = buckets.GetOrAdd(key, _ => new TokenBucket(capacity: 20, refillPerSecond: 10));
    if (!bucket.TryTake(DateTimeOffset.UtcNow))
    {
        ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        return;
    }
    await next();
});

// AuthZ na borda: papel + atributos decidem a rota ANTES do proxy encaminhar.
app.Use(async (ctx, next) =>
{
    var table = ctx.RequestServices.GetRequiredService<RouteTable>();
    var match = table.Match(ctx.Request.Path);

    if (!match.IsListed)
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound; // rota não listada nem existe pra fora
        return;
    }

    if (!match.IsPublic)
    {
        if (ctx.User.Identity?.IsAuthenticated != true)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var subject = ClaimsMapper.ToSubject(
            ctx.User.FindFirstValue("sub") ?? "unknown",
            ctx.User.Claims.Select(c => (c.Type, c.Value)));

        var decision = AccessPolicy.Evaluate(subject, match.Requirement!);
        if (decision != AccessDecision.Allow)
        {
            instrumentation.Meter.CreateCounter<long>("gateway.authz.denied")
                .Add(1, new KeyValuePair<string, object?>("reason", decision.ToString()));
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
    }

    await next();
});

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapReverseProxy();

app.Run();
