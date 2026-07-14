using Edge.ProtocolGateway.Domain.Security;
using Xunit;

namespace Edge.ProtocolGateway.Tests;

public class DeviceIdentityTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
    private static DeviceCertificate Cert(string id, string tp, int fromDays = -1, int toDays = 30) =>
        new(id, tp, Now.AddDays(fromDays), Now.AddDays(toDays));

    [Fact]
    public void Device_nao_enrolado_e_desconhecido()
    {
        var registry = new DeviceRegistry();
        Assert.Equal(DeviceTrust.Unknown, registry.Evaluate("tp-x", Now));
        Assert.False(registry.IsAllowed("tp-x", Now));
    }

    [Fact]
    public void Device_enrolado_e_valido_e_confiavel()
    {
        var registry = new DeviceRegistry();
        registry.Enroll(Cert("prensa-3", "tp-1"));
        Assert.Equal(DeviceTrust.Trusted, registry.Evaluate("tp-1", Now));
    }

    [Fact]
    public void Device_revogado_e_recusado_mesmo_dentro_da_validade()
    {
        var registry = new DeviceRegistry();
        registry.Enroll(Cert("prensa-3", "tp-1"));
        registry.Revoke("tp-1"); // comprometido

        Assert.Equal(DeviceTrust.Revoked, registry.Evaluate("tp-1", Now));
        Assert.False(registry.IsAllowed("tp-1", Now));
    }

    [Fact]
    public void Cert_expirado_ou_futuro_nao_e_confiavel()
    {
        var registry = new DeviceRegistry();
        registry.Enroll(Cert("s1", "tp-exp", fromDays: -40, toDays: -10));
        registry.Enroll(Cert("s2", "tp-fut", fromDays: 5, toDays: 40));

        Assert.Equal(DeviceTrust.Expired, registry.Evaluate("tp-exp", Now));
        Assert.Equal(DeviceTrust.NotYetValid, registry.Evaluate("tp-fut", Now));
    }

    [Fact]
    public void Reenrollment_reativa_um_thumbprint_revogado()
    {
        var registry = new DeviceRegistry();
        registry.Enroll(Cert("s1", "tp-1"));
        registry.Revoke("tp-1");
        registry.Enroll(Cert("s1", "tp-1", toDays: 60)); // cert novo, mesmo thumbprint
        Assert.Equal(DeviceTrust.Trusted, registry.Evaluate("tp-1", Now));
    }

    [Fact]
    public void Certs_a_expirar_sao_listados_para_rotacao()
    {
        var registry = new DeviceRegistry();
        registry.Enroll(Cert("s1", "tp-soon", toDays: 3));
        registry.Enroll(Cert("s2", "tp-later", toDays: 60));

        var expiring = registry.ExpiringWithin(TimeSpan.FromDays(7), Now);
        Assert.Equal(["tp-soon"], expiring.Select(c => c.Thumbprint));
    }

    [Theory]
    [InlineData("1.2.0", "1.3.0", OtaState.UpdateAvailable)]
    [InlineData("1.3.0", "1.3.0", OtaState.UpToDate)]
    [InlineData("2.0.0", "1.9.9", OtaState.UpToDate)]      // nunca rebaixa
    [InlineData("1.2", "1.2.5", OtaState.UpdateAvailable)] // segmento ausente = 0
    public void Ota_decide_por_versao_semantica(string current, string target, OtaState expected)
    {
        var planner = new OtaPlanner([new FirmwareTarget("edge-gw", target)]);
        Assert.Equal(expected, planner.Decide("edge-gw", current).State);
    }

    [Fact]
    public void Ota_modelo_sem_alvo_e_unsupported()
    {
        var planner = new OtaPlanner([new FirmwareTarget("edge-gw", "1.0.0")]);
        Assert.Equal(OtaState.Unsupported, planner.Decide("modelo-x", "1.0.0").State);
    }
}
