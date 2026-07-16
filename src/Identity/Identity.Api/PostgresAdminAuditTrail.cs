using Platform.Audit;

namespace Identity.Api;

/// <summary>
/// Sink de auditoria da FASE 1: grava o evento durável no outbox Postgres antes de
/// devolver sucesso ao admin; um relay o leva ao Kafka (auditoria.admin.v1) e daí
/// pro lake WORM.
/// </summary>
public sealed class PostgresAdminAuditTrail(AdminAuditStore store) : IAdminAuditTrail
{
    public const string Topic = "auditoria.admin.v1";

    public async Task RecordAsync(AdminAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        // CREATE TABLE IF NOT EXISTS é idempotente e barato, e a ação administrativa
        // é rara: garantir o schema a cada emissão evita estado (e o CA1001 de um
        // campo descartável) sem custo relevante. O relay também garante no startup.
        await store.EnsureSchemaAsync(cancellationToken);
        await store.AppendAsync(auditEvent, Topic, cancellationToken);
    }
}
