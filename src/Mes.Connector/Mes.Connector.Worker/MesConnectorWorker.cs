using Mes.Connector.Domain;

namespace Mes.Connector.Worker;

/// <summary>
/// Loop de coleta: poll do adapter → só o que é novo (cursor) → normaliza → Kafka.
/// Invariantes:
///   1. Nunca perde: linha que não normaliza vai pra quarentena com o motivo.
///   2. Nunca reprocessa: o cursor (idempotência) filtra o que já passou.
///   3. Cursor em memória na fase 0 (reinício pode re-pollar; consumidores deduplicam por
///      event_id). Fase 1 persiste o cursor.
/// </summary>
public sealed partial class MesConnectorWorker(
    MesOptions options,
    IMesAdapter adapter,
    MesEventSink sink,
    ILogger<MesConnectorWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStart(options.EventTopic, options.SourceSystem);
        var state = MesPollState.Start;

        while (!stoppingToken.IsCancellationRequested)
        {
            var rows = await adapter.PollAsync(state.LastCursor, stoppingToken);
            var (fresh, next) = PollCursor.SelectNew(rows, state);

            foreach (var row in fresh)
            {
                using var activity = MesTelemetry.Activity.StartActivity("mes.event");
                var result = MesNormalizer.Normalize(row, options.SourceSystem, Guid.NewGuid);

                if (result.Accepted && result.Event is { } evento)
                {
                    activity?.SetTag("ativo.id", evento.AtivoId);
                    await sink.PublishAsync(evento, stoppingToken);
                    MesTelemetry.Published.Add(1);
                }
                else
                {
                    await sink.QuarantineAsync(row, result.Reason, stoppingToken);
                    MesTelemetry.Quarantined.Add(1, new KeyValuePair<string, object?>("reason", result.Reason));
                    LogQuarantined(row.Cursor, result.Reason);
                }
            }

            state = next;
            await Task.Delay(options.PollInterval, stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Conector MES iniciado → {Topic} (origem {Source})")]
    private partial void LogStart(string topic, string source);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Linha MES em quarentena: {Cursor} · {Reason}")]
    private partial void LogQuarantined(string cursor, string reason);
}
