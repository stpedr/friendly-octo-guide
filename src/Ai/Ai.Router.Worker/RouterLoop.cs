using System.Text.Json;
using Ai.Domain.Jobs;
using Confluent.Kafka;
using Platform.ServiceDefaults;

namespace Ai.Router.Worker;

public sealed partial class RouterLoop(
    ServiceInstrumentation instrumentation,
    IConfiguration config,
    ILogger<RouterLoop> log) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrap = config["Kafka:Bootstrap"] ?? "localhost:9092";
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId = "ai-router",
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        }).Build();
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrap,
            EnableIdempotence = true,
        }).Build();

        consumer.Subscribe(config["Kafka:JobsTopic"] ?? "ai.jobs.v1");
        var routed = instrumentation.Meter.CreateCounter<long>("ai.router.routed");

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = consumer.Consume(stoppingToken);
            using var activity = instrumentation.Activity.StartActivity("ai.route");

            AiJob? job = null;
            try
            {
                job = JsonSerializer.Deserialize<AiJob>(result.Message.Value, JsonOpts);
            }
            catch (JsonException)
            {
                // cai no caso nulo abaixo
            }

            var decision = job is null
                ? RouteDecision.DeadLetter("Envelope de job indecodificável.")
                : DispatchPolicy.Decide(job);

            activity?.SetTag("ai.model_type", job?.ModelType);
            activity?.SetTag("ai.route", decision.Topic);

            var headers = new Headers();
            if (decision.Reason is not null)
                headers.Add("reason", System.Text.Encoding.UTF8.GetBytes(decision.Reason));

            await producer.ProduceAsync(decision.Topic, new Message<string, string>
            {
                Key = job?.JobId.ToString() ?? result.Message.Key,
                Value = result.Message.Value, // payload intacto: worker (ou replay da DLQ) recebe o original
                Headers = headers,
            }, stoppingToken);

            routed.Add(1, new KeyValuePair<string, object?>("route", decision.Topic));
            if (decision.Kind == RouteKind.DeadLetter)
                LogDeadLettered(job?.JobId, decision.Reason!);

            consumer.Commit(result); // só depois do produce confirmado
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} → DLQ: {Reason}")]
    private partial void LogDeadLettered(Guid? jobId, string reason);
}
