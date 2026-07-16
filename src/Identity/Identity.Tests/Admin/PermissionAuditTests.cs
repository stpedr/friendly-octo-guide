using Identity.Api;
using Platform.Audit;
using Xunit;

namespace Identity.Tests.Admin;

public class PermissionAuditTests
{
    [Fact]
    public void ApplyPermissionChange_devolve_before_e_after_do_usuario()
    {
        var store = new InMemoryUserStore();

        var change = store.ApplyPermissionChange(
            "operador",
            ["operador", "supervisor"],
            new Dictionary<string, string> { ["planta"] = "A", ["linha"] = "2", ["turno"] = "dia" });

        Assert.NotNull(change);
        Assert.Contains("operador", change!.Before.Roles);
        Assert.DoesNotContain("supervisor", change.Before.Roles);
        Assert.Contains("supervisor", change.After.Roles);
    }

    [Fact]
    public void ApplyPermissionChange_persiste_para_a_proxima_leitura()
    {
        var store = new InMemoryUserStore();

        store.ApplyPermissionChange("operador", ["operador", "supervisor"],
            new Dictionary<string, string> { ["planta"] = "B" });

        var user = store.Find("operador");
        Assert.NotNull(user);
        Assert.Contains("supervisor", user!.Roles);
        Assert.Equal("B", user.Attributes["planta"]);
    }

    [Fact]
    public void ApplyPermissionChange_usuario_inexistente_devolve_null()
    {
        var store = new InMemoryUserStore();

        var change = store.ApplyPermissionChange("ninguem", [], new Dictionary<string, string>());

        Assert.Null(change);
    }

    [Fact]
    public void Mudanca_de_permissao_vira_evento_auditavel_sem_vazar_seed()
    {
        var store = new InMemoryUserStore();

        var change = store.ApplyPermissionChange(
            "operador",
            ["operador", "admin"],
            new Dictionary<string, string> { ["planta"] = "A", ["totpSeed"] = "nao-deve-aparecer" })!;

        var auditEvent = AdminAuditEvents.ForPermissionChange(
            actor: "msuchoa", actorRoles: ["admin"], targetUser: "operador",
            before: change.Before, after: change.After, occurredAt: DateTimeOffset.UtcNow);

        Assert.Equal(AdminAction.PermissionChanged, auditEvent.Action);
        Assert.Equal("operador", auditEvent.TargetId);
        Assert.Contains("admin", auditEvent.After!);
        Assert.DoesNotContain("nao-deve-aparecer", auditEvent.After!);
    }
}
