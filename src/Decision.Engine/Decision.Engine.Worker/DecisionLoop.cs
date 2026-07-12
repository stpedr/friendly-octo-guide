using System.Text.Json;
using Confluent.Kafka;
using Decision.Engine.Domain.Guardrails;
using Platform.ServiceDefaults;

namespace Decision.Engine.Worker;

public sealed record CommandProposal(
    Guid CommandId, string ActuatorId, double TargetValue, double CurrentValue, Criticality Criticality);

/// <summary>
/// Consome propostas (do Predictive/agentes) em linha.comandos.propostos.v1 e:
///   - AutoApproved  → linha.comandos.aprovados.v1 (o edge gateway executa no PLC)
///   - NeedsHuman    → linha.comandos.pendentes.v1 (painel admin aprova/recusa)
///   - Rejected      → só auditoria — fisicamente inválido não circula
/// TODA decisão, em qualquer desfecho, vai pra auditoria.decisoes.v1 com trace-id.
/// </summary>
public sealed partial class DecisionLoop(
    OperatingEnvelope envelope,
    ServiceInstrumentation instrumentation,
    IConfiguration config,
    ILogger<DecisionLoop> log) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrap = config["Kafka:Bootstrap"] ?? "localhost:9092";
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId = "decision-engine",
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        }).Build();
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrap,
            EnableIdempotence = true,
        }).Build();

        consumer.Subscribe(config["Kafka:ProposalsTopic"] ?? "linha.comandos.propostos.v1");
        var decisions = instrumentation.Meter.CreateCounter<long>("decision.outcomes");

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = consumer.Consume(stoppingToken);
            using var activity = instrumentation.Activity.StartActivity("decision.evaluate");

            var proposal = JsonSerializer.Deserialize<CommandProposal>(result.Message.Value, JsonOpts);
            if (proposal is null)
            {
                consumer.Commit(result);
                continue;
            }

            var command = new ProposedCommand(
                proposal.CommandId, proposal.ActuatorId, proposal.TargetValue, proposal.CurrentValue);
            var decision = ApprovalPolicy.Decide(envelope.Check(command), proposal.Criticality);

            activity?.SetTag("decision.outcome", decision.Outcome.ToString());
            activity?.SetTag("actuator.id", proposal.ActuatorId);
            decisions.Add(1, new KeyValuePair<string, object?>("outcome", decision.Outcome.ToString()));

            // Auditoria SEMPRE — com trace-id pra jornada completa no Tempo.
            var audit = JsonSerializer.Serialize(new
            {
                proposal.CommandId,
                proposal.ActuatorId,
                proposal.TargetValue,
                outcome = decision.Outcome.ToString(),
                decision.Rationale,
                traceId = activity?.TraceId.ToString(),
                decidedAt = DateTimeOffset.UtcNow,
            }, JsonOpts);
            await producer.ProduceAsync(config["Kafka:AuditTopic"] ?? "auditoria.decisoes.v1",
                new Message<string, string> { Key = proposal.CommandId.ToString(), Value = audit }, stoppingToken);

            var destination = decision.Outcome switch
            {
                DecisionOutcome.AutoApproved => config["Kafka:ApprovedTopic"] ?? "linha.comandos.aprovados.v1",
                DecisionOutcome.NeedsHumanApproval => config["Kafka:PendingTopic"] ?? "linha.comandos.pendentes.v1",
                _ => null, // rejeitado morre na auditoria
            };
            if (destination is not null)
            {
                await producer.ProduceAsync(destination,
                    new Message<string, string> { Key = proposal.CommandId.ToString(), Value = result.Message.Value },
                    stoppingToken);
            }

            LogDecision(proposal.CommandId, proposal.ActuatorId, decision.Outcome);
            consumer.Commit(result);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Decisão {Outcome} pro comando {CommandId} ({Actuator})")]
    private partial void LogDecision(Guid commandId, string actuator, DecisionOutcome outcome);
}
