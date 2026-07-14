using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Gateway.IntegrationTests;

/// <summary>
/// Prova, pela pipeline HTTP REAL do Gateway (WebApplicationFactory), que a
/// plataforma suporta os DOIS modos por configuração, sem recompilar:
///   - MultiTenant:Enabled=false → single-tenant (o header X-Tenant é ignorado);
///   - MultiTenant:Enabled=true  → exige X-Tenant e nega cross-tenant.
/// "Passou o gate" = o request atravessou authz+tenant e chegou no proxy (que sem
/// downstream real devolve 5xx) — o que importa é NÃO ser 400/401/403.
/// </summary>
public sealed class MultiTenantModeTests
{
    private const string SigningKey = "dev-only-signing-key-with-32-bytes!!";

    private static WebApplicationFactory<Program> Factory(bool multiTenant) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MultiTenant:Enabled"] = multiTenant ? "true" : "false",
                ["Jwt:SigningKey"] = SigningKey,
            }));
        });

    private static string Token(string tenant)
    {
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: "identity", audience: "plataforma-linha",
            claims:
            [
                new Claim("sub", "u1"),
                new Claim("role", "operador"),
                new Claim("attr:tenant", tenant),
            ],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private static HttpRequestMessage Get(string token, string? tenantHeader)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/v1/knowledge/graphql");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (tenantHeader is not null)
            req.Headers.Add("X-Tenant", tenantHeader);
        return req;
    }

    private static bool PassedGate(HttpStatusCode s) =>
        s is not (HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);

    [Fact]
    public async Task Single_tenant_ignora_o_header_e_deixa_passar()
    {
        using var factory = Factory(multiTenant: false);
        using var client = factory.CreateClient();

        var resp = await client.SendAsync(Get(Token("acme"), tenantHeader: null));
        Assert.True(PassedGate(resp.StatusCode), $"esperava passar o gate, veio {(int)resp.StatusCode}");
    }

    [Fact]
    public async Task Multi_tenant_sem_header_e_400()
    {
        using var factory = Factory(multiTenant: true);
        using var client = factory.CreateClient();

        var resp = await client.SendAsync(Get(Token("acme"), tenantHeader: null));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Multi_tenant_cross_tenant_e_403()
    {
        using var factory = Factory(multiTenant: true);
        using var client = factory.CreateClient();

        // Usuário do tenant "acme" tentando recurso do tenant "beta".
        var resp = await client.SendAsync(Get(Token("acme"), tenantHeader: "beta"));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Multi_tenant_mesmo_tenant_passa()
    {
        using var factory = Factory(multiTenant: true);
        using var client = factory.CreateClient();

        var resp = await client.SendAsync(Get(Token("acme"), tenantHeader: "acme"));
        Assert.True(PassedGate(resp.StatusCode), $"esperava passar o gate, veio {(int)resp.StatusCode}");
    }
}
