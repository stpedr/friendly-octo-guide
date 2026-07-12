using Notifications.Domain.Escalation;
using Notifications.Worker;
using Platform.ServiceDefaults;

// Consome alertas do Kafka e despacha por canal seguindo a política de on-call.
// Push nativo via ntfy (OSS, self-hosted) — chega no app Tauri do bloco de observabilidade.

var instrumentation = new ServiceInstrumentation("notifications");

var builder = Host.CreateApplicationBuilder(args);
builder.AddPlatformDefaults(instrumentation);

builder.Services.AddSingleton(instrumentation);
builder.Services.AddHttpClient<NtfyPusher>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Ntfy:BaseUrl"] ?? "http://localhost:8090"));
builder.Services.AddSingleton<EmailSender>();
builder.Services.AddSingleton(new EscalationPolicy(
    new Dictionary<Severity, IReadOnlyList<EscalationLevel>>
    {
        // Escada padrão; fase 1 carrega do Postgres com edição no painel admin.
        [Severity.Critical] =
        [
            new("oncall-primario", TimeSpan.FromMinutes(5)),
            new("oncall-secundario", TimeSpan.FromMinutes(10)),
            new("gestor", TimeSpan.FromMinutes(15)),
        ],
        [Severity.Warning] = [new("oncall-primario", TimeSpan.FromHours(1))],
    }));
builder.Services.AddHostedService<AlertConsumer>();

await builder.Build().RunAsync();
