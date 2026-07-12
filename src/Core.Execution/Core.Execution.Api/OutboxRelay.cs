using Confluent.Kafka;
using Core.Execution.Domain.Outbox;
using Platform.ServiceDefaults;

namespace Core.Execution.Api;

/// <summary>
/// Drena o outbox pro Kafka. A política (ordem, backoff, lote) é do domínio;
/// aqui só tem IO. Publicação é at-least-once — o consumidor deduplica pelo id
/// do evento, que viaja como chave da mensagem.
/// </summary>
public sealed partial class OutboxRelay(
    WorkOrderStore store,
    ServiceInstrumentation instrumentation,
    IConfiguration config,
    ILogger<OutboxRelay> log) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await store.EnsureSchemaAsync(stoppingToken);

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config["Kafka:Bootstrap"] ?? "localhost:9092",
            EnableIdempotence = true, // sem duplicata nem reordenação do lado do broker
        };
        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();
        var publishedCounter = instrumentation.Meter.CreateCounter<long>("outbox.published");
        var pendingGauge = instrumentation.Meter.CreateGauge<long>("outbox.pending");

        while (!stoppingToken.IsCancellationRequested)
        {
            var pending = await store.PendingOutboxAsync(stoppingToken);
            pendingGauge.Record(pending.Count);

            foreach (var message in OutboxRelayPolicy.DueBatch(pending, DateTimeOffset.UtcNow, BatchSize))
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao publicar evento {MessageId} (tentativa {Attempt}) — backoff em curso")]
    private partial void LogPublishFailed(Exception ex, Guid messageId, int attempt);
}
