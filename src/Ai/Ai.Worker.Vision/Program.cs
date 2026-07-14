using Ai.Worker.Runtime;
using Ai.Worker.Vision;
using Platform.ServiceDefaults;

// Worker de visão/OCR: um Deployment K8s isolado, GPU node pool, scale-to-zero via
// KEDA por lag de ai.jobs.vision.v1. Serving auto-hospedado — zero custo por token.

var instrumentation = new ServiceInstrumentation("ai-worker-vision");

var builder = Host.CreateApplicationBuilder(args);
builder.AddPlatformDefaults(instrumentation);

builder.Services.AddSingleton(instrumentation);
builder.Services.AddHttpClient<IJobProcessor, VisionProcessor>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Vision:BaseUrl"] ?? "http://localhost:8001");
    c.Timeout = TimeSpan.FromMinutes(3);
});
builder.Services.AddHostedService<AiJobLoop>();

await builder.Build().RunAsync();
