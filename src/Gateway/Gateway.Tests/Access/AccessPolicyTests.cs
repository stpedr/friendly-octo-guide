using Platform.AccessControl;
using Xunit;

namespace Gateway.Tests.Access;

public class AccessPolicyTests
{
    private static Subject Operador(params (string k, string v)[] attrs) => new(
        "user-1",
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "operador" },
        attrs.ToDictionary(a => a.k, a => a.v));

    [Fact]
    public void Papel_certo_sem_exigencia_de_atributo_permite()
    {
        var decision = AccessPolicy.Evaluate(
            Operador(),
            RouteRequirement.ForRoles("operador", "admin"));

        Assert.Equal(AccessDecision.Allow, decision);
    }

    [Fact]
    public void Sem_o_papel_exigido_nega_por_papel()
    {
        var decision = AccessPolicy.Evaluate(
            Operador(),
            RouteRequirement.ForRoles("admin"));

        Assert.Equal(AccessDecision.DenyMissingRole, decision);
    }

    [Fact]
    public void Atributo_de_planta_errado_nega_mesmo_com_papel_certo()
    {
        // Multi-planta: operador da planta A não enxerga rota da planta B.
        var req = new RouteRequirement(
            new HashSet<string> { "operador" },
            new Dictionary<string, string> { ["planta"] = "B" });

        var decision = AccessPolicy.Evaluate(Operador(("planta", "A")), req);

        Assert.Equal(AccessDecision.DenyMissingAttribute, decision);
    }

    [Fact]
    public void Curinga_exige_presenca_do_atributo_com_qualquer_valor()
    {
        var req = new RouteRequirement(
            new HashSet<string> { "operador" },
            new Dictionary<string, string> { ["linha"] = "*" });

        Assert.Equal(AccessDecision.Allow, AccessPolicy.Evaluate(Operador(("linha", "2")), req));
        Assert.Equal(AccessDecision.DenyMissingAttribute, AccessPolicy.Evaluate(Operador(), req));
    }

    [Fact]
    public void Comparacao_de_atributo_ignora_caixa()
    {
        var req = new RouteRequirement(
            new HashSet<string> { "operador" },
            new Dictionary<string, string> { ["turno"] = "NOITE" });

        Assert.Equal(AccessDecision.Allow, AccessPolicy.Evaluate(Operador(("turno", "noite")), req));
    }
}
