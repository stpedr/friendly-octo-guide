using System.Text.Json;
using Identity.Api;
using Platform.Audit;
using Xunit;

namespace Identity.Tests.Admin;

public class AdminAuditPayloadTests
{
    private static AdminAuditEvent Sample() => AdminAuditEvents.ForPermissionChange(
        actor: "msuchoa",
        actorRoles: ["admin"],
        targetUser: "operador",
        before: new PermissionSnapshot(
            new HashSet<string>(StringComparer.Ordinal) { "operador" },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["planta"] = "A" }),
        after: new PermissionSnapshot(
            new HashSet<string>(StringComparer.Ordinal) { "operador", "admin" },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["planta"] = "A", ["totpSeed"] = "segredo" }),
        occurredAt: DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
        traceId: "trace-1");

    [Fact]
    public void Serialize_produz_json_com_os_campos_do_contrato()
    {
        using var doc = JsonDocument.Parse(AdminAuditPayload.Serialize(Sample()));
        var root = doc.RootElement;

        Assert.Equal("msuchoa", root.GetProperty("actor").GetString());
        Assert.Equal(AdminAction.PermissionChanged, root.GetProperty("action").GetString());
        Assert.Equal("operador", root.GetProperty("targetId").GetString());
        Assert.Equal("trace-1", root.GetProperty("traceId").GetString());
    }

    [Fact]
    public void Serialize_nunca_vaza_segredo_no_payload()
    {
        var json = AdminAuditPayload.Serialize(Sample());

        Assert.DoesNotContain("segredo", json);
        Assert.Contains(AuditRedaction.Placeholder, json);
    }
}
