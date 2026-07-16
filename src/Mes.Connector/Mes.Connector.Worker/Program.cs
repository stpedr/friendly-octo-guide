using System.Diagnostics;
using System.Diagnostics.Metrics;
using Mes.Connector.Domain;
using Mes.Connector.Worker;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

// Conector MES (nível Purdue 3/4): poll do MES → normaliza → Kafka mes.eventos.v1.
// Caminho próprio, paralelo ao Edge.ProtocolGateway (chão de fábrica). Template de
// host do monorepo: nasce com log estruturado, traces e métricas no OTel Collector.

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(cfg => cfg
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("service", MesTelemetry.ServiceName)
    .WriteTo.Console(formatProvider: null));

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(MesTelemetry.ServiceName))
    .WithTracing(t => t
        .AddSource(MesTelemetry.ServiceName)
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddMeter(MesTelemetry.ServiceName)
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

builder.Services.AddSingleton(MesOptions.From(builder.Configuration));
builder.Services.AddSingleton<MesEventSink>();
// Adapter genérico: o simulador roda em dev. Um RestMesAdapter/SqlMesAdapter pluga
// aqui implementando IMesAdapter quando o MES real existir — sem tocar no domínio.
builder.Services.AddSingleton<IMesAdapter, SimulatorMesAdapter>();
builder.Services.AddHostedService<MesConnectorWorker>();

await builder.Build().RunAsync();

namespace Mes.Connector.Worker
{
    /// <summary>Fonte única de nomes de instrumentação do serviço.</summary>
    public static class MesTelemetry
    {
        public const string ServiceName = "mes-connector";
        public static readonly ActivitySource Activity = new(ServiceName);
        public static readonly Meter Meter = new(ServiceName);

        public static readonly Counter<long> Published =
            Meter.CreateCounter<long>("mes.events.published");
        public static readonly Counter<long> Quarantined =
            Meter.CreateCounter<long>("mes.events.quarantined");
    }

    public sealed record MesOptions(
        string KafkaBootstrap,
        string EventTopic,
        string QuarantineTopic,
        string SourceSystem,
        TimeSpan PollInterval)
    {
        public static MesOptions From(IConfiguration cfg) => new(
            cfg["Kafka:Bootstrap"] ?? "localhost:9092",
            cfg["Kafka:EventTopic"] ?? "mes.eventos.v1",
            cfg["Kafka:QuarantineTopic"] ?? "mes.quarentena.v1",
            cfg["Mes:SourceSystem"] ?? "simulador",
            TimeSpan.FromSeconds(cfg.GetValue("Mes:PollIntervalSeconds", 5)));
    }
}
