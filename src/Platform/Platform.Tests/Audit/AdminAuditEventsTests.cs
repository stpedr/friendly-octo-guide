using Platform.Audit;
using Xunit;

namespace Platform.Tests.Audit;

public class AdminAuditEventsTests
{
    private static readonly DateTimeOffset When = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
    private static readonly Guid FixedId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static PermissionSnapshot Snap(string[] roles, params (string Key, string Value)[] attrs) =>
        new(roles.ToHashSet(StringComparer.Ordinal),
            attrs.ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal));

    [Fact]
    public void ForPermissionChange_monta_evento_com_acao_e_alvo_certos()
    {
        var before = Snap(["operador"], ("planta", "A"));
        var after = Snap(["operador", "admin"], ("planta", "A"));

        var evt = AdminAuditEvents.ForPermissionChange(
            actor: "msuchoa", actorRoles: ["admin"], targetUser: "operador",
            before, after, When, traceId: "trace-abc", newId: () => FixedId);

        Assert.Equal(FixedId, evt.EventId);
        Assert.Equal("msuchoa", evt.Actor);
        Assert.Equal(new[] { "admin" }, evt.ActorRoles);
        Assert.Equal(AdminAction.PermissionChanged, evt.Action);
        Assert.Equal(AuditTargetType.User, evt.TargetType);
        Assert.Equal("operador", evt.TargetId);
        Assert.Equal("trace-abc", evt.TraceId);
        Assert.Equal(When, evt.OccurredAt);
    }

    [Fact]
    public void ForPermissionChange_grava_before_e_after_redigidos()
    {
        var before = Snap(["operador"], ("totpSeed", "antigo"));
        var after = Snap(["operador"], ("totpSeed", "novo"));

        var evt = AdminAuditEvents.ForPermissionChange(
            "msuchoa", ["admin"], "operador", before, after, When, newId: () => FixedId);

        // a seed nunca aparece em claro na trilha, nem no antes nem no depois.
        Assert.DoesNotContain("antigo", evt.Before!);
        Assert.DoesNotContain("novo", evt.After!);
        Assert.Contains(AuditRedaction.Placeholder, evt.After!);
    }

    [Fact]
    public void ForPermissionChange_recusa_nao_mudanca()
    {
        var same = Snap(["operador"], ("planta", "A"));

        Assert.Throws<InvalidOperationException>(() =>
            AdminAuditEvents.ForPermissionChange("msuchoa", ["admin"], "operador", same, same, When));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ForPermissionChange_exige_ator(string actor)
    {
        var before = Snap(["operador"]);
        var after = Snap(["operador", "admin"]);

        Assert.Throws<ArgumentException>(() =>
            AdminAuditEvents.ForPermissionChange(actor, ["admin"], "operador", before, after, When));
    }

    [Fact]
    public void ForPermissionChange_exige_alvo()
    {
        var before = Snap(["operador"]);
        var after = Snap(["operador", "admin"]);

        Assert.Throws<ArgumentException>(() =>
            AdminAuditEvents.ForPermissionChange("msuchoa", ["admin"], "", before, after, When));
    }
}
