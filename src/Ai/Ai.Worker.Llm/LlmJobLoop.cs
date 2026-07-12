using System.Diagnostics;
using System.Text.Json;
using Ai.Domain.Jobs;
using Confluent.Kafka;
using Platform.ServiceDefaults;

namespace Ai.Worker.Llm;

/// <summary>
/// Consome ai.jobs.llm.v1 → vLLM → ai.resultados.v1.
/// Idempotência por job-id (reprocesso nunca duplica resultado); falha de inferência
/// reencaminha o job com attempts+1 — o router decide se ainda cabe retry ou se é DLQ.
/// </summary>
public sealed partial class LlmJobLoop(
    VllmClient vllm,
    ServiceInstrumentation instrumentation,
    IConfiguration config,
    ILogger<LlmJobLoop> log) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly IdempotencyLedger _ledger = new(); // fase 1: Valkey SET NX — vale entre réplicas

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrap = config["Kafka:Bootstrap"] ?? "localhost:9092";
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId = "ai-worker-llm",
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        }).Build();
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrap,
            EnableIdempotence = true,
        }).Build();

        consumer.Subscribe(config["Kafka:LlmJobsTopic"] ?? "ai.jobs.llm.v1");
        var inferenceSeconds = instrumentation.Meter.CreateHistogram<double>("ai.inference.seconds");
        var failures = instrumentation.Meter.CreateCounter<long>("ai.inference.failures");

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = consumer.Consume(stoppingToken);
            using var activity = instrumentation.Activity.StartActivity("ai.llm.job");

            var job = JsonSerializer.Deserialize<AiJob>(result.Message.Value, JsonOpts);
            if (job is null)
            {
                consumer.Commit(result); // router já teria mandado pra DLQ; aqui é cinto e suspensório
                continue;
            }

            activity?.SetTag("ai.job_id", job.JobId);

            if (_ledger.TryClaim(job.JobId) != JobClaim.Accepted)
            {
                consumer.Commit(result); // duplicata de entrega — resultado já existe ou está em voo
                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var answer = await vllm.CompleteAsync(job.Payload, stoppingToken);
                inferenceSeconds.Record(stopwatch.Elapsed.TotalSeconds);

                await producer.ProduceAsync(config["Kafka:ResultsTopic"] ?? "ai.resultados.v1",
                    new Message<string, string>
                    {
                        Key = job.JobId.ToString(),
                        Value = JsonSerializer.Serialize(new { jobId = job.JobId, answer }, JsonOpts),
                    }, stoppingToken);

                _ledger.Complete(job.JobId);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
            {
                failures.Add(1);
                _ledger.Release(job.JobId); // devolve o claim: retry é legítimo
                LogInferenceFailed(ex, job.JobId, job.Attempts + 1);

                // Reencaminha pro funil do router com a tentativa contada — DLQ se esgotar.
                var retry = job with { Attempts = job.Attempts + 1 };
                await producer.ProduceAsync(config["Kafka:JobsTopic"] ?? "ai.jobs.v1",
                    new Message<string, string>
                    {
                        Key = retry.JobId.ToString(),
                        Value = JsonSerializer.Serialize(retry, JsonOpts),
                    }, stoppingToken);
            }

            consumer.Commit(result); // job foi concluído OU reenfileirado — nunca perdido
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Inferência falhou pro job {JobId} (tentativa {Attempt})")]
    private partial void LogInferenceFailed(Exception ex, Guid jobId, int attempt);
}
