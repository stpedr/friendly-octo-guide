using Platform.Audit;
using Xunit;

namespace Platform.Tests.Audit;

public class PermissionSnapshotTests
{
    private static PermissionSnapshot Snap(string[] roles, params (string Key, string Value)[] attrs) =>
        new(roles.ToHashSet(StringComparer.Ordinal),
            attrs.ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal));

    [Fact]
    public void ToAuditString_ordena_e_redige()
    {
        var snap = Snap(["operador", "admin"], ("planta", "A"), ("totpSeed", "xyz"));

        // papéis e atributos em ordem canônica; a seed vira placeholder.
        Assert.Equal(
            $"roles=[admin,operador] attrs=[planta=A,totpSeed={AuditRedaction.Placeholder}]",
            snap.ToAuditString());
    }

    [Fact]
    public void ToAuditString_e_estavel_independente_da_ordem_de_insercao()
    {
        var a = Snap(["admin", "operador"], ("linha", "2"), ("planta", "A"));
        var b = Snap(["operador", "admin"], ("planta", "A"), ("linha", "2"));

        Assert.Equal(a.ToAuditString(), b.ToAuditString());
    }

    [Fact]
    public void SamePermissions_verdadeiro_para_mesmo_conteudo_em_ordem_diferente()
    {
        var a = Snap(["admin", "operador"], ("planta", "A"));
        var b = Snap(["operador", "admin"], ("planta", "A"));

        Assert.True(a.SamePermissions(b));
    }

    [Fact]
    public void SamePermissions_falso_quando_papel_muda()
    {
        var a = Snap(["operador"], ("planta", "A"));
        var b = Snap(["operador", "admin"], ("planta", "A"));

        Assert.False(a.SamePermissions(b));
    }

    [Fact]
    public void SamePermissions_falso_quando_atributo_muda()
    {
        var a = Snap(["operador"], ("planta", "A"));
        var b = Snap(["operador"], ("planta", "B"));

        Assert.False(a.SamePermissions(b));
    }

    [Fact]
    public void Empty_nao_tem_papel_nem_atributo()
    {
        Assert.Empty(PermissionSnapshot.Empty.Roles);
        Assert.Empty(PermissionSnapshot.Empty.Attributes);
    }
}
