namespace Decision.Engine.Domain.Guardrails;

/// <summary>Comando proposto de volta pra linha: um atuador, um setpoint alvo.</summary>
public sealed record ProposedCommand(Guid CommandId, string ActuatorId, double TargetValue, double CurrentValue);

/// <summary>
/// Envelope de operação de um atuador: os limites FÍSICOS permitidos e o maior
/// degrau por comando. O modelo pode sugerir o que quiser; o que sai daqui
/// nunca ultrapassa o envelope — guardrail é física, não palpite.
/// </summary>
public sealed record ActuatorEnvelope(double Min, double Max, double MaxStepChange);

public enum EnvelopeVerdict { WithinEnvelope, OutOfRange, StepTooLarge, UnknownActuator }

public sealed class OperatingEnvelope(IReadOnlyDictionary<string, ActuatorEnvelope> envelopes)
{
    public EnvelopeVerdict Check(ProposedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!envelopes.TryGetValue(command.ActuatorId, out var envelope))
            return EnvelopeVerdict.UnknownActuator; // atuador não cadastrado nem recebe comando

        if (double.IsNaN(command.TargetValue) ||
            command.TargetValue < envelope.Min || command.TargetValue > envelope.Max)
            return EnvelopeVerdict.OutOfRange;

        if (Math.Abs(command.TargetValue - command.CurrentValue) > envelope.MaxStepChange)
            return EnvelopeVerdict.StepTooLarge; // mudança brusca é risco mesmo dentro da faixa

        return EnvelopeVerdict.WithinEnvelope;
    }
}
