using Platform.ServiceDefaults;
using Predictive.Worker;

// Scoring online sobre o stream de telemetria: anomalia vira alerta ANTES do
// problema ocorrer (acatech estágio 5). Um detector por sensor, estado em memória —
// particionamento por sensor no Kafka garante afinidade quando escalar.

var instrumentation = new ServiceInstrumentation("predictive");

var builder = Host.CreateApplicationBuilder(args);
builder.AddPlatformDefaults(instrumentation);

builder.Services.AddSingleton(instrumentation);

// MLflow (Model Registry) opcional: com Mlflow:BaseUrl configurado, o worker
// resolve a versão ativa no boot e registra runs de recalibração. Sem ele
// (dev), o scoring roda igual — a proveniência entra quando o registry sobe.
if (!string.IsNullOrEmpty(builder.Configuration["Mlflow:BaseUrl"]))
{
    builder.Services.AddHttpClient<MlflowClient>(c =>
        c.BaseAddress = new Uri(builder.Configuration["Mlflow:BaseUrl"]!));
    builder.Services.AddHostedService<ModelBootstrap>();
}

builder.Services.AddHostedService<ScoringLoop>();

await builder.Build().RunAsync();
