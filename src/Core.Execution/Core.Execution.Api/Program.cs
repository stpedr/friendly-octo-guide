using Core.Execution.Api;
using Core.Execution.Domain.WorkOrders;
using Platform.ServiceDefaults;

// Serviço de domínio: regras do produto (ordens de produção) + outbox pattern.
// Contrato síncrono versionado (/v1); o assíncrono sai pelo outbox → core.eventos.v1.

var instrumentation = new ServiceInstrumentation("core-execution");

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults(instrumentation);

builder.Services.AddSingleton(instrumentation);
builder.Services.AddSingleton(new WorkOrderStore(
    builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Database=linha;Username=dev;Password=dev"));
builder.Services.AddHostedService<OutboxRelay>();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

var ordens = app.MapGroup("/v1/core/ordens");

ordens.MapPost("/", async (CreateOrderRequest req, WorkOrderStore store, CancellationToken ct) =>
{
    try
    {
        var order = new WorkOrder(Guid.NewGuid(), req.Line, req.Product, req.Quantity);
        await store.InsertAsync(order, evt: null, ct);
        return Results.Created($"/v1/core/ordens/{order.Id}", ToDto(order));
    }
    catch (DomainException ex)
    {
        return Results.UnprocessableEntity(new { error = ex.Message });
    }
});

ordens.MapGet("/{id:guid}", async (Guid id, WorkOrderStore store, CancellationToken ct) =>
    await store.FindAsync(id, ct) is { } order ? Results.Ok(ToDto(order)) : Results.NotFound());

ordens.MapPost("/{id:guid}/liberar", (Guid id, WorkOrderStore s, CancellationToken ct) =>
    TransitionAsync(id, s, o => o.Release(DateTimeOffset.UtcNow), ct));
ordens.MapPost("/{id:guid}/iniciar", (Guid id, WorkOrderStore s, CancellationToken ct) =>
    TransitionAsync(id, s, o => o.Start(DateTimeOffset.UtcNow), ct));
ordens.MapPost("/{id:guid}/concluir", (Guid id, WorkOrderStore s, CancellationToken ct) =>
    TransitionAsync(id, s, o => o.Complete(DateTimeOffset.UtcNow), ct));
ordens.MapPost("/{id:guid}/abortar", (Guid id, WorkOrderStore s, CancellationToken ct) =>
    TransitionAsync(id, s, o => o.Abort(DateTimeOffset.UtcNow), ct));

app.Run();

static async Task<IResult> TransitionAsync(
    Guid id, WorkOrderStore store, Func<WorkOrder, WorkOrderEvent> transition, CancellationToken ct)
{
    if (await store.FindAsync(id, ct) is not { } order)
        return Results.NotFound();

    try
    {
        var evt = transition(order); // domínio valida; estado + evento na mesma transação abaixo
        await store.ApplyTransitionAsync(order, evt, ct);
        return Results.Ok(ToDto(order));
    }
    catch (DomainException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
}

static object ToDto(WorkOrder order) => new
{
    id = order.Id,
    line = order.Line,
    product = order.Product,
    quantity = order.Quantity,
    state = order.State.ToString(),
};

public sealed record CreateOrderRequest(string Line, string Product, int Quantity);
