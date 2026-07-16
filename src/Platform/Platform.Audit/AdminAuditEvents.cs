namespace Platform.Audit;

/// <summary>
/// Constrói o evento de auditoria de uma ação — o núcleo que todo emissor reusa,
/// pra que a trilha seja consistente venha de onde vier. Guarda as invariantes
/// (ator e alvo obrigatórios), redige valores sensíveis e devolve o registro
/// append-only. Puro: não grava nada — quem persiste é o IAdminAuditTrail.
/// </summary>
public static class AdminAuditEvents
{
    /// <summary>
    /// Mudança de permissão de um usuário. before/after saem redigidos e
    /// ordenados. Lança se nada mudou — auditar não-mudança é ruído que polui a
    /// trilha; o chamador deve checar <see cref="PermissionSnapshot.SamePermissions"/>
    /// antes. <paramref name="newId"/> permite id determinístico em teste.
    /// </summary>
    public static AdminAuditEvent ForPermissionChange(
        string actor,
        IReadOnlyList<string> actorRoles,
        string targetUser,
        PermissionSnapshot before,
        PermissionSnapshot after,
        DateTimeOffset occurredAt,
        string? traceId = null,
        Func<Guid>? newId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUser);
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        if (before.SamePermissions(after))
            throw new InvalidOperationException("Mudança de permissão sem diferença — nada a auditar.");

        var idFactory = newId ?? Guid.NewGuid;
        return new AdminAuditEvent(
            EventId: idFactory(),
            Actor: actor,
            ActorRoles: [.. actorRoles],
            Action: AdminAction.PermissionChanged,
            TargetType: AuditTargetType.User,
            TargetId: targetUser,
            Before: before.ToAuditString(),
            After: after.ToAuditString(),
            TraceId: traceId,
            OccurredAt: occurredAt);
    }
}
