using System.Security.Claims;
using Chatbot.Api;
using Chatbot.Domain.Tools;
using Platform.AccessControl;
using Xunit;

namespace Chatbot.Tests;

/// <summary>
/// A camada MCP não inventa política: toda ferramenta exposta passa pelos
/// mesmos guardrails do registro. Estes testes cobrem o que a fase MCP mudou —
/// a ferramenta nova no registro e o mapeamento claims → Subject que alimenta
/// o RBAC das chamadas de agente.
/// </summary>
public class McpToolsTests
{
    private static Subject Operador() => new(
        "user-1",
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "operador" },
        new Dictionary<string, string>());

    [Fact]
    public void Buscar_conhecimento_e_leitura_liberada_para_operador()
    {
        var access = ToolRegistry.Default().Evaluate("buscar_conhecimento", Operador(), humanConfirmed: false);
        Assert.Equal(ToolAccess.Allowed, access);
    }

    [Fact]
    public void Buscar_conhecimento_e_negada_fora_do_rbac()
    {
        var visitante = new Subject("user-2",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "visitante" },
            new Dictionary<string, string>());

        var access = ToolRegistry.Default().Evaluate("buscar_conhecimento", visitante, humanConfirmed: false);
        Assert.Equal(ToolAccess.DeniedByRbac, access);
    }

    [Fact]
    public void Ferramenta_destrutiva_continua_exigindo_confirmacao_humana_via_mcp()
    {
        // O transporte mudou (MCP), o guardrail não: abortar sem humano confirmar → 409.
        var access = ToolRegistry.Default().Evaluate("abortar_ordem", Operador(), humanConfirmed: false);
        Assert.Equal(ToolAccess.NeedsHumanConfirmation, access);
    }

    [Fact]
    public void SubjectFrom_mapeia_sub_papeis_e_atributos_das_claims()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-9"),
            new Claim("role", "operador"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim("attr:linha", "2"),
            new Claim("attr:turno", "noite"),
            new Claim("email", "nao-vira-atributo@exemplo.com"),
        ], authenticationType: "test"));

        var subject = PlataformaTools.SubjectFrom(user);

        Assert.Equal("user-9", subject.UserId);
        Assert.True(subject.Roles.SetEquals(["operador", "admin"]));
        Assert.Equal(new Dictionary<string, string> { ["linha"] = "2", ["turno"] = "noite" }, subject.Attributes);
    }
}
