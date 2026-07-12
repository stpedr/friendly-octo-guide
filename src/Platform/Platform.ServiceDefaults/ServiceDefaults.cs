using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace Platform.ServiceDefaults;

/// <summary>
/// Espinha de observabilidade em forma de código: um único ponto onde log estruturado,
/// traces e métricas são ligados. Nenhum dado trafega, é gerado ou é perdido sem
/// passar por ela — porque nenhum host do monorepo sobe sem chamar este método.
/// </summary>
public static class PlatformDefaults
{
    /// <summary>
    /// Liga Serilog (JSON em prod via appsettings) + OpenTelemetry (OTLP → Collector).
    /// O endpoint vem de OTEL_EXPORTER_OTLP_ENDPOINT — mesmo contrato em dev e no cluster.
    /// </summary>
    public static TBuilder AddPlatformDefaults<TBuilder>(this TBuilder builder, ServiceInstrumentation instrumentation)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSerilog(cfg => cfg
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.WithProperty("service", instrumentation.ServiceName)
            .WriteTo.Console(formatProvider: null));

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(instrumentation.ServiceName))
            .WithTracing(t => t
                .AddSource(instrumentation.ServiceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(m => m
                .AddMeter(instrumentation.ServiceName)
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter());

        return builder;
    }
}

/// <summary>
/// Fonte única de nomes de instrumentação de um serviço: um ActivitySource e um Meter,
/// ambos com o nome do serviço — é o que o Collector usa pra rotear.
/// </summary>
public sealed class ServiceInstrumentation(string serviceName) : IDisposable
{
    public string ServiceName { get; } = serviceName;
    public ActivitySource Activity { get; } = new(serviceName);
    public Meter Meter { get; } = new(serviceName);

    public void Dispose()
    {
        Activity.Dispose();
        Meter.Dispose();
    }
}
