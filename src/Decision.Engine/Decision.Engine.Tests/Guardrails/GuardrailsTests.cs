using Decision.Engine.Domain.Guardrails;
using Xunit;

namespace Decision.Engine.Tests.Guardrails;

public class OperatingEnvelopeTests
{
    // Válvula do forno: 0–100%, no máximo 10 pontos por comando.
    private static readonly OperatingEnvelope Envelope = new(
        new Dictionary<string, ActuatorEnvelope>
        {
            ["valvula-forno-01"] = new(Min: 0, Max: 100, MaxStepChange: 10),
        });

    private static ProposedCommand Cmd(double target, double current = 50, string actuator = "valvula-forno-01") =>
        new(Guid.NewGuid(), actuator, target, current);

    [Fact]
    public void Comando_dentro_do_envelope_passa()
    {
        Assert.Equal(EnvelopeVerdict.WithinEnvelope, Envelope.Check(Cmd(target: 55)));
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(120)]
    [InlineData(double.NaN)]
    public void Alvo_fora_da_faixa_fisica_e_recusado(double target)
    {
        Assert.Equal(EnvelopeVerdict.OutOfRange, Envelope.Check(Cmd(target)));
    }

    [Fact]
    public void Degrau_brusco_e_recusado_mesmo_dentro_da_faixa()
    {
        // 50 → 90: dentro de 0–100, mas 40 pontos de uma vez estressa o processo.
        Assert.Equal(EnvelopeVerdict.StepTooLarge, Envelope.Check(Cmd(target: 90, current: 50)));
    }

    [Fact]
    public void Atuador_desconhecido_nao_recebe_comando()
    {
        Assert.Equal(EnvelopeVerdict.UnknownActuator, Envelope.Check(Cmd(50, actuator: "valvula-fantasma")));
    }
}

public class ApprovalPolicyTests
{
    [Fact]
    public void Envelope_ok_e_criticidade_baixa_executa_sozinho()
    {
        var d = ApprovalPolicy.Decide(EnvelopeVerdict.WithinEnvelope, Criticality.Low);
        Assert.Equal(DecisionOutcome.AutoApproved, d.Outcome);
    }

    [Theory]
    [InlineData(Criticality.Medium)]
    [InlineData(Criticality.High)]
    public void Criticidade_alta_exige_humano_mesmo_dentro_do_envelope(Criticality criticality)
    {
        var d = ApprovalPolicy.Decide(EnvelopeVerdict.WithinEnvelope, criticality);
        Assert.Equal(DecisionOutcome.NeedsHumanApproval, d.Outcome);
    }

    [Theory]
    [InlineData(EnvelopeVerdict.OutOfRange)]
    [InlineData(EnvelopeVerdict.StepTooLarge)]
    [InlineData(EnvelopeVerdict.UnknownActuator)]
    public void Fora_do_envelope_e_rejeitado_antes_de_chegar_a_um_humano(EnvelopeVerdict verdict)
    {
        // Criticidade não importa: fisicamente inválido não é decisão, é erro.
        var d = ApprovalPolicy.Decide(verdict, Criticality.Low);
        Assert.Equal(DecisionOutcome.Rejected, d.Outcome);
        Assert.Contains(verdict.ToString(), d.Rationale, StringComparison.Ordinal);
    }
}
