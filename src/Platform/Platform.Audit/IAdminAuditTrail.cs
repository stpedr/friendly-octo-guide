namespace Platform.Audit;

/// <summary>
/// Destino da trilha administrativa — append-only, sempre.
///
/// Fase 1: a implementação grava via OUTBOX (na mesma transação da ação, igual
/// ao Core.Execution) → tópico auditoria.admin.v1 → Data.Archiver → bucket WORM
/// no MinIO, com retenção longa e separada do log de acesso operacional.
///
/// Fase 0: implementação em log estruturado (LoggingAdminAuditTrail). O contrato
/// NÃO muda entre fases — o emissor sempre chama RecordAsync.
/// </summary>
public interface IAdminAuditTrail
{
    Task RecordAsync(AdminAuditEvent auditEvent, CancellationToken cancellationToken = default);
}
