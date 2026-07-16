using System.Diagnostics;
using Platform.Audit;

namespace Identity.Api;

public sealed record PermissionChangeRequest(string[]? Roles, Dictionary<string, string>? Attributes);

/// <summary>
/// Ações administrativas sobre usuários. TODA mudança de permissão vira trilha de
/// auditoria (auditoria.admin.v1) — append-only, redigida, correlacionada por
/// trace-id. Em prod estas rotas ficam atrás do RBAC do Gateway (papel admin);
/// aqui a rota confia no ator do TOKEN, nunca no corpo.
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/v1/admin");

        admin.MapPost("/users/{username}/permissions",
            async (string username, PermissionChangeRequest req, IUserAdmin users, IAdminAuditTrail audit, HttpContext ctx, CancellationToken ct) =>
        {
            ArgumentNullException.ThrowIfNull(req);

            var change = users.ApplyPermissionChange(
                username, req.Roles ?? [], req.Attributes ?? new Dictionary<string, string>());
            if (change is null)
                return Results.NotFound();

            // Auditar não-mudança é ruído: se nada mudou, no-op sem evento.
            if (change.Before.SamePermissions(change.After))
                return Results.Ok(new { changed = false });

            var auditEvent = AdminAuditEvents.ForPermissionChange(
                actor: ActorOf(ctx),
                actorRoles: RolesOf(ctx),
                targetUser: username,
                before: change.Before,
                after: change.After,
                occurredAt: DateTimeOffset.UtcNow,
                traceId: Activity.Current?.TraceId.ToString());

            await audit.RecordAsync(auditEvent, ct);
            return Results.Ok(new { changed = true, auditEventId = auditEvent.EventId });
        });
    }

    // O ator vem SEMPRE do token validado (identidade que o Gateway injeta),
    // NUNCA do corpo — auditar com ator forjável não audita nada. Sem auth no dev
    // local, o ator é o processo; em prod ctx.User está populado.
    private static string ActorOf(HttpContext ctx) =>
        ctx.User.Identity?.Name is { Length: > 0 } name ? name : "system:dev";

    private static IReadOnlyList<string> RolesOf(HttpContext ctx) =>
        [.. ctx.User.FindAll("role").Select(c => c.Value)];
}
