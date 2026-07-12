using Ai.Worker.Llm;
using Platform.ServiceDefaults;

// Worker de LLM: um Deployment K8s por modelo, GPU node pool, scale-to-zero via KEDA.
// Inferência no vLLM auto-hospedado (API OpenAI-compatible) — zero custo por token.

var instrumentation = new ServiceInstrumentation("ai-worker-llm");

var builder = Host.CreateApplicationBuilder(args);
builder.AddPlatformDefaults(instrumentation);

builder.Services.AddSingleton(instrumentation);
builder.Services.AddHttpClient<VllmClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Vllm:BaseUrl"] ?? "http://localhost:8000");
    c.Timeout = TimeSpan.FromMinutes(5); // inferência longa não é timeout de rede
});
builder.Services.AddHostedService<LlmJobLoop>();

await builder.Build().RunAsync();
