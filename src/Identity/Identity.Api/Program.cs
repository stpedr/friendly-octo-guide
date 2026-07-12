using Identity.Api;
using Platform.ServiceDefaults;

var instrumentation = new ServiceInstrumentation("identity");

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults(instrumentation);

builder.Services.AddSingleton(instrumentation);
builder.Services.AddSingleton<IUserStore, InMemoryUserStore>(); // fase 1: Postgres (tabela users + totp_seed cifrada via pgcrypto)
builder.Services.AddSingleton<TokenIssuer>();
builder.Services.AddSingleton<LoginFlow>();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();

app.Run();
