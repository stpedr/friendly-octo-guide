namespace Ai.Domain.Jobs;

/// <summary>Envelope do job na fila — o contrato entre serviços .NET e o subsistema de IA.</summary>
public sealed record AiJob(Guid JobId, string ModelType, string Payload, int Attempts);

public enum RouteKind { Forward, DeadLetter }

public sealed record RouteDecision(RouteKind Kind, string Topic, string? Reason)
{
    public static RouteDecision Forward(string topic) => new(RouteKind.Forward, topic, null);
    public static RouteDecision DeadLetter(string reason) => new(RouteKind.DeadLetter, DispatchPolicy.DlqTopic, reason);
}

/// <summary>
/// O router não roda modelo: decide PRA ONDE o job vai. Tipo conhecido → tópico do
/// worker certo; tipo desconhecido ou tentativas esgotadas → DLQ com motivo.
/// Job com falha nunca some — DLQ é fila, não lixeira.
/// </summary>
public static class DispatchPolicy
{
    public const string DlqTopic = "ai.jobs.dlq.v1";
    public const int MaxAttempts = 3;

    private static readonly Dictionary<string, string> TopicByModelType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["llm"] = "ai.jobs.llm.v1",
        ["vision"] = "ai.jobs.vision.v1",
        ["embedding"] = "ai.jobs.embedding.v1",
    };

    public static RouteDecision Decide(AiJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (job.Attempts >= MaxAttempts)
            return RouteDecision.DeadLetter($"Tentativas esgotadas ({job.Attempts}/{MaxAttempts}).");

        return TopicByModelType.TryGetValue(job.ModelType, out var topic)
            ? RouteDecision.Forward(topic)
            : RouteDecision.DeadLetter($"Tipo de modelo desconhecido: '{job.ModelType}'.");
    }
}
