using Ai.Router.Worker;
using Platform.ServiceDefaults;

// Router/Dispatcher do subsistema de IA: lê ai.jobs.v1, decide o tipo de modelo
// e encaminha pro tópico do worker certo. Falha vai pra DLQ com motivo — nunca some.
// KEDA escala este worker (e os de modelo) pela profundidade da fila.

var instrumentation = new ServiceInstrumentation("ai-router");

var builder = Host.CreateApplicationBuilder(args);
builder.AddPlatformDefaults(instrumentation);

builder.Services.AddSingleton(instrumentation);
builder.Services.AddHostedService<RouterLoop>();

await builder.Build().RunAsync();
