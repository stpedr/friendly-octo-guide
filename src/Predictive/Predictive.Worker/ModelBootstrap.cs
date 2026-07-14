using Platform.ServiceDefaults;

namespace Predictive.Worker;

/// <summary>
/// No boot, resolve no Model Registry qual versão do modelo está em Production e a
/// registra na telemetria — o worker deixa de "servir um modelo qualquer" e passa a
/// saber (e emitir) exatamente qual versão está no ar. Falha do registry não
/// impede o scoring: degrada pra "versão desconhecida".
/// </summary>
public sealed partial class ModelBootstrap(
    MlflowClient mlflow,
    ServiceInstrumentation instrumentation,
    IConfiguration config,
    ILogger<ModelBootstrap> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var modelName = config["Mlflow:ModelName"] ?? "predictive-anomaly";
        try
        {
            var version = await mlflow.ActiveModelVersionAsync(modelName, stoppingToken);
            instrumentation.Activity.StartActivity("predictive.model.resolved")
                ?.SetTag("model.version", version ?? "unknown");
            LogResolved(modelName, version ?? "desconhecida (nenhuma em Production)");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            LogRegistryUnavailable(ex, modelName);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Modelo {Model} ativo: versão {Version}")]
    private partial void LogResolved(string model, string version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Model Registry indisponível pra {Model}; seguindo sem versão resolvida")]
    private partial void LogRegistryUnavailable(Exception ex, string model);
}
