using Decision.Engine.Domain.Guardrails;
using Decision.Engine.Worker;
using Platform.ServiceDefaults;

// Fecha o loop de volta pra linha (acatech estágio 6) — com guardrails:
// envelope físico primeiro, aprovação humana por criticidade depois.
// O comando aprovado volta pelo MESMO edge gateway, auditado e reversível.

var instrumentation = new ServiceInstrumentation("decision-engine");

var builder = Host.CreateApplicationBuilder(args);
builder.AddPlatformDefaults(instrumentation);

builder.Services.AddSingleton(instrumentation);
builder.Services.AddSingleton(new OperatingEnvelope(new Dictionary<string, ActuatorEnvelope>
{
    // Envelopes vêm do cadastro da linha na fase 1; fixos aqui pra PoC do loop.
    ["valvula-forno-01"] = new(Min: 0, Max: 100, MaxStepChange: 10),
    ["esteira-linha-02"] = new(Min: 0, Max: 2.5, MaxStepChange: 0.5),
}));
builder.Services.AddHostedService<DecisionLoop>();

await builder.Build().RunAsync();
