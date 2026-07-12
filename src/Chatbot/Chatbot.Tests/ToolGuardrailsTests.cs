using Chatbot.Domain.Tools;
using Platform.AccessControl;
using Xunit;

namespace Chatbot.Tests;

public class ToolGuardrailsTests
{
    private static readonly ToolGuardrails Guardrails = new(
        new Dictionary<string, ToolDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["consultar_ordem"] = new("consultar_ordem", ToolKind.Read,
                RouteRequirement.ForRoles("operador", "admin")),
            ["abortar_ordem"] = new("abortar_ordem", ToolKind.DestructiveAct,
                RouteRequirement.ForRoles("operador", "admin")),
            ["gerir_usuarios"] = new("gerir_usuarios", ToolKind.DestructiveAct,
                RouteRequirement.ForRoles("admin")),
        });

    private static Subject Operador() => new(
        "maria", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "operador" },
        new Dictionary<string, string>());

    [Fact]
    public void Leitura_e_livre_dentro_do_rbac()
    {
        Assert.Equal(ToolAccess.Allowed,
            Guardrails.Evaluate("consultar_ordem", Operador(), humanConfirmed: false));
    }

    [Fact]
    public void Acao_destrutiva_sem_confirmacao_humana_e_barrada()
    {
        // permission policy always_ask: o agente NUNCA aborta ordem sozinho.
        Assert.Equal(ToolAccess.NeedsHumanConfirmation,
            Guardrails.Evaluate("abortar_ordem", Operador(), humanConfirmed: false));
    }

    [Fact]
    public void Acao_destrutiva_confirmada_pelo_humano_passa()
    {
        Assert.Equal(ToolAccess.Allowed,
            Guardrails.Evaluate("abortar_ordem", Operador(), humanConfirmed: true));
    }

    [Fact]
    public void Rbac_barra_antes_da_confirmacao_importar()
    {
        // Operador não gere usuários nem com confirmação: o agente não excede o dono da sessão.
        Assert.Equal(ToolAccess.DeniedByRbac,
            Guardrails.Evaluate("gerir_usuarios", Operador(), humanConfirmed: true));
    }

    [Fact]
    public void Ferramenta_nao_registrada_nao_existe_pro_agente()
    {
        Assert.Equal(ToolAccess.UnknownTool,
            Guardrails.Evaluate("dropar_banco", Operador(), humanConfirmed: true));
    }
}
