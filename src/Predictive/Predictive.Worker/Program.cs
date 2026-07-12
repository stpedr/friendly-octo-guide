using Platform.ServiceDefaults;
using Predictive.Worker;

// Scoring online sobre o stream de telemetria: anomalia vira alerta ANTES do
// problema ocorrer (acatech estágio 5). Um detector por sensor, estado em memória —
// particionamento por sensor no Kafka garante afinidade quando escalar.

var instrumentation = new ServiceInstrumentation("predictive");

var builder = Host.CreateApplicationBuilder(args);
builder.AddPlatformDefaults(instrumentation);

builder.Services.AddSingleton(instrumentation);
builder.Services.AddHostedService<ScoringLoop>();

await builder.Build().RunAsync();
