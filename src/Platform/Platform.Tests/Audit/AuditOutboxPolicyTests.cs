using Platform.Audit;
using Xunit;

namespace Platform.Tests.Audit;

public class AuditOutboxPolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    private static AuditOutboxMessage Msg(int attempts = 0, DateTimeOffset? occurredAt = null, DateTimeOffset? publishedAt = null) =>
        new(Guid.NewGuid(), "auditoria.admin.v1", "{}", occurredAt ?? Now, publishedAt, attempts);

    [Fact]
    public void Mensagem_nova_esta_pronta_imediatamente()
    {
        Assert.Equal(Now, AuditOutboxPolicy.NextAttemptAt(Msg(attempts: 0)));
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 8)]
    [InlineData(3, 16)]
    public void Backoff_e_exponencial(int attempts, int expectedSeconds)
    {
        var next = AuditOutboxPolicy.NextAttemptAt(Msg(attempts));
        Assert.Equal(Now + TimeSpan.FromSeconds(expectedSeconds), next);
    }

    [Fact]
    public void Backoff_tem_teto_de_cinco_minutos()
    {
        Assert.Equal(Now + TimeSpan.FromMinutes(5), AuditOutboxPolicy.NextAttemptAt(Msg(attempts: 30)));
    }

    [Fact]
    public void Lote_ignora_publicadas_e_ainda_nao_devidas()
    {
        var publicada = Msg(publishedAt: Now);
        var emBackoff = Msg(attempts: 5);
        var pronta = Msg();

        var batch = AuditOutboxPolicy.DueBatch([publicada, emBackoff, pronta], Now, batchSize: 10);

        Assert.Equal([pronta], batch);
    }

    [Fact]
    public void Lote_sai_em_ordem_de_ocorrencia_e_respeita_o_tamanho()
    {
        var antiga = Msg(occurredAt: Now - TimeSpan.FromMinutes(2));
        var media = Msg(occurredAt: Now - TimeSpan.FromMinutes(1));
        var nova = Msg(occurredAt: Now);

        var batch = AuditOutboxPolicy.DueBatch([nova, antiga, media], Now, batchSize: 2);

        Assert.Equal([antiga, media], batch);
    }
}
