using Ai.Domain.Jobs;

namespace Ai.Worker.Runtime;

// Sem Worker SDK aqui (é biblioteca): as global usings de Microsoft.Extensions.*
// vêm por import explícito no AiJobLoop, não por convenção do SDK.

/// <summary>
/// O que varia entre workers de IA: de qual tópico consomem, o grupo, e como
/// transformam um job em resultado. Todo o resto — idempotência, retry contado,
/// DLQ pelo router, publicação do resultado, métricas — é do <see cref="AiJobLoop"/>,
/// escrito uma vez só. Worker novo = um processor novo, não um loop novo.
/// </summary>
public interface IJobProcessor
{
    /// <summary>Grupo de consumo Kafka (um por tipo de worker).</summary>
    string ConsumerGroup { get; }

    /// <summary>Tópico que este worker consome (ex.: ai.jobs.vision.v1).</summary>
    string JobsTopic { get; }

    /// <summary>Roda o modelo e devolve o payload do resultado (JSON serializável).</summary>
    Task<object> ProcessAsync(AiJob job, CancellationToken ct);
}
