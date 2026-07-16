using Platform.Audit;
using Xunit;

namespace Platform.Tests.Audit;

public class AuditRedactionTests
{
    [Theory]
    [InlineData("senha")]
    [InlineData("Password")]
    [InlineData("passwordHash")]
    [InlineData("totpSeed")]
    [InlineData("api_key")]
    [InlineData("clientSecret")]
    [InlineData("refreshToken")]
    public void Chave_sensivel_e_detectada_sem_ligar_pra_caixa(string key)
    {
        Assert.True(AuditRedaction.IsSensitiveKey(key));
    }

    [Theory]
    [InlineData("planta")]
    [InlineData("linha")]
    [InlineData("turno")]
    [InlineData("role")]
    public void Chave_comum_nao_e_sensivel(string key)
    {
        Assert.False(AuditRedaction.IsSensitiveKey(key));
    }

    [Fact]
    public void Redact_substitui_valor_sensivel_e_preserva_o_resto()
    {
        var redacted = AuditRedaction.Redact(new Dictionary<string, string>
        {
            ["planta"] = "A",
            ["totpSeed"] = "dev-seed-msuchoa-0123",
            ["password"] = "w1ntersun",
        });

        Assert.Equal("A", redacted["planta"]);
        Assert.Equal(AuditRedaction.Placeholder, redacted["totpSeed"]);
        Assert.Equal(AuditRedaction.Placeholder, redacted["password"]);
    }

    [Fact]
    public void Redact_de_mapa_vazio_e_vazio()
    {
        Assert.Empty(AuditRedaction.Redact(new Dictionary<string, string>()));
    }
}
