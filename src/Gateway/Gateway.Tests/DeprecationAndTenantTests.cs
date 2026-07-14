using Gateway.Domain;
using Platform.AccessControl;
using Xunit;

namespace Gateway.Tests;

public class DeprecationTableTests
{
    private static readonly DateTimeOffset Sunset = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Rota_nao_deprecada_nao_ganha_cabecalho()
    {
        var table = new DeprecationTable().Deprecate("/v1/core", Sunset, "/v2/core");
        Assert.Null(table.For("/v2/core/ordens"));
    }

    [Fact]
    public void Rota_deprecada_anuncia_sunset_e_sucessor()
    {
        var table = new DeprecationTable().Deprecate("/v1/core", Sunset, "/v2/core");

        var headers = table.For("/v1/core/ordens");

        Assert.NotNull(headers);
        Assert.Equal("true", headers!.Deprecation);
        Assert.Equal("Fri, 01 Jan 2027 00:00:00 GMT", headers.Sunset); // HTTP-date da RFC 8594
        Assert.Equal("/v2/core", headers.Successor);
    }

    [Fact]
    public void Prefixo_mais_longo_vence()
    {
        var table = new DeprecationTable()
            .Deprecate("/v1", Sunset)
            .Deprecate("/v1/core", Sunset.AddYears(1), "/v2/core");

        Assert.Equal("/v2/core", table.For("/v1/core/x")!.Successor);
    }
}

public class TenantPolicyTests
{
    private static Subject Sub(string? tenant) => new(
        "u1",
        new HashSet<string> { "operador" },
        tenant is null ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["tenant"] = tenant });

    [Fact]
    public void Desligado_tudo_passa_independente_do_tenant()
        => Assert.Equal(TenantDecision.Allow, TenantPolicy.Evaluate(Sub("acme"), "outra-empresa", enabled: false));

    [Fact]
    public void Ligado_mesmo_tenant_passa()
        => Assert.Equal(TenantDecision.Allow, TenantPolicy.Evaluate(Sub("acme"), "acme", enabled: true));

    [Fact]
    public void Ligado_tenant_diferente_e_negado()
        => Assert.Equal(TenantDecision.DenyCrossTenant, TenantPolicy.Evaluate(Sub("acme"), "beta", enabled: true));

    [Fact]
    public void Ligado_sem_claim_de_tenant_e_negado()
        => Assert.Equal(TenantDecision.DenyNoTenant, TenantPolicy.Evaluate(Sub(null), "acme", enabled: true));
}
