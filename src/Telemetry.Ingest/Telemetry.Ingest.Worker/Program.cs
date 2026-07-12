using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Telemetry.Ingest.Worker;

// Template de host do monorepo: todo serviço nasce com log estruturado,
// traces e métricas apontando pro OTel Collector — a "espinha" recebe
// desde o primeiro deploy, sem exceção.

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(cfg => cfg
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("service", IngestTelemetry.ServiceName)
    .WriteTo.Console(formatProvider: null)); // JSON via appsettings em prod

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(IngestTelemetry.ServiceName))
    .WithTracing(t => t
        .AddSource(IngestTelemetry.ServiceName)
        .AddOtlpExporter()) // OTEL_EXPORTER_OTLP_ENDPOINT via env
    .WithMetrics(m => m
        .AddMeter(IngestTelemetry.ServiceName)
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

builder.Services.AddSingleton(IngestOptions.From(builder.Configuration));
builder.Services.AddHostedService<IngestConsumer>();

await builder.Build().RunAsync();

namespace Telemetry.Ingest.Worker
{
    /// <summary>Fonte única de nomes de instrumentação do serviço.</summary>
    public static class IngestTelemetry
    {
        public const string ServiceName = "telemetry-ingest";
        public static readonly ActivitySource Activity = new(ServiceName);
        public static readonly Meter Meter = new(ServiceName);

        // As três métricas que o painel da linha consome:
        public static readonly Counter<long> Accepted =
            Meter.CreateCounter<long>("ingest.readings.accepted");
        public static readonly Counter<long> Quarantined =
            Meter.CreateCounter<long>("ingest.readings.quarantined");
        public static readonly Histogram<double> LagSeconds =
            Meter.CreateHistogram<double>("ingest.lag.seconds");
    }

    public sealed record IngestOptions(
        string KafkaBootstrap,
        string TelemetryTopic,
        string QuarantineTopic,
        string PostgresConnection)
    {
        public static IngestOptions From(IConfiguration cfg) => new(
            cfg["Kafka:Bootstrap"] ?? "localhost:9092",
            cfg["Kafka:TelemetryTopic"] ?? "linha.telemetria.v1",
            cfg["Kafka:QuarantineTopic"] ?? "linha.telemetria.quarentena.v1",
            cfg.GetConnectionString("Postgres") ?? "Host=localhost;Database=linha;Username=dev;Password=dev");
    }
}
