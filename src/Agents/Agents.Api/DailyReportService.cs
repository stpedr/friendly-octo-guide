using System.Text.Json;
using Agents.Domain.Reporting;
using Confluent.Kafka;
using Platform.ServiceDefaults;

namespace Agents.Api;

/// <summary>
/// Agente agendado: uma vez por dia consolida os sinais da janela num relatório e
/// publica em relatorios.diarios.v1 (o Notifications entrega). Não decide nada —
/// só observa e resume, então roda sem guardrail de ação.
/// </summary>
public sealed partial class DailyReportService(
    SignalWindow window,
    ServiceInstrumentation instrumentation,
    IConfiguration config,
    ILogger<DailyReportService> log) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hourUtc = config.GetValue("Agents:DailyReportHourUtc", 6);
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = config["Kafka:Bootstrap"] ?? "localhost:9092",
            EnableIdempotence = true,
        }).Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(DelayUntilNext(DateTimeOffset.UtcNow, hourUtc), stoppingToken);

            var day = DateOnly.FromDateTime(DateTime.UtcNow);
            var report = DailyReportBuilder.Build(window.Snapshot(), day);
            using var activity = instrumentation.Activity.StartActivity("agents.daily_report");
            activity?.SetTag("report.incidents", report.Incidents);

            await producer.ProduceAsync(config["Kafka:ReportsTopic"] ?? "relatorios.diarios.v1",
                new Message<string, string>
                {
                    Key = day.ToString("O"),
                    Value = JsonSerializer.Serialize(report, JsonOpts),
                }, stoppingToken);
            LogPublished(day, report.Incidents, report.TotalSignals);
        }
    }

    /// <summary>Tempo até a próxima ocorrência da hora-alvo (UTC). Exposto pra teste.</summary>
    internal static TimeSpan DelayUntilNext(DateTimeOffset now, int hourUtc)
    {
        var next = new DateTimeOffset(now.Year, now.Month, now.Day, hourUtc, 0, 0, TimeSpan.Zero);
        if (next <= now)
            next = next.AddDays(1);
        return next - now;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Relatório diário {Day}: {Incidents} incidentes, {Total} sinais")]
    private partial void LogPublished(DateOnly day, int incidents, int total);
}
