using Confluent.Kafka;
using Platform.Audit;
using Platform.ServiceDefaults;

namespace Identity.Api;

/// <summary>
/// Drena o outbox de auditoria pro Kafka. A política (ordem, backoff, lote) é do
/// domínio (AuditOutboxPolicy); aqui só tem IO. Publicação at-least-once — o
/// consumidor (Data.Archiver → WORM) deduplica pelo event_id, que viaja como chave.
/// Mesma forma do OutboxRelay do Core.Execution.
/// </summary>
public sealed partial class AdminAuditOutboxRelay(
    AdminAuditStore store,
    ServiceInstrumentation instrumentation,
    IConfiguration config,
    ILogger<AdminAuditOutboxRelay> log) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await store.EnsureSchemaAsync(stoppingToken);

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config["Kafka:Bootstrap"] ?? "localhost:9092",
            EnableIdempotence = true,
        };
        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();
        var publishedCounter = instrumentation.Meter.CreateCounter<long>("audit.outbox.published");
        var pendingGauge = instrumentation.Meter.CreateGauge<long>("audit.outbox.pending");

        while (!stoppingToken.IsCancellationRequested)
        {
            var pending = await store.PendingAsync(stoppingToken);
            pendingGauge.Record(pending.Count);

            foreach (var message in AuditOutboxPolicy.DueBatch(pending, DateTimeOffset.UtcNow, BatchSize))
            {
                try
                {
                    await producer.ProduceAsync(message.Topic,
                        new Message<string, string> { Key = message.Id.ToString(), Value = message.Payload },
                        stoppingToken);
                    await store.MarkPublishedAsync(message.Id, DateTimeOffset.UtcNow, stoppingToken);
                    publishedCounter.Add(1);
                }
                catch (ProduceException<string, string> ex)
                {
                    await store.RecordAttemptAsync(message.Id, stoppingToken);
                    LogPublishFailed(ex, message.Id, message.Attempts + 1);
                }
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao publicar evento de auditoria {MessageId} (tentativa {Attempt}) — backoff em curso")]
    private partial void LogPublishFailed(Exception ex, Guid messageId, int attempt);
}
