using Ai.Worker.Embeddings;
using Ai.Worker.Runtime;
using Platform.ServiceDefaults;

// Worker de embeddings: Deployment K8s isolado, scale-to-zero via KEDA por lag de
// ai.jobs.embedding.v1. Alimenta pgvector/RAG — vetorização auto-hospedada.

var instrumentation = new ServiceInstrumentation("ai-worker-embeddings");

var builder = Host.CreateApplicationBuilder(args);
builder.AddPlatformDefaults(instrumentation);

builder.Services.AddSingleton(instrumentation);
builder.Services.AddHttpClient<IJobProcessor, EmbeddingsProcessor>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Embeddings:BaseUrl"] ?? "http://localhost:8002");
    c.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddHostedService<AiJobLoop>();

await builder.Build().RunAsync();
