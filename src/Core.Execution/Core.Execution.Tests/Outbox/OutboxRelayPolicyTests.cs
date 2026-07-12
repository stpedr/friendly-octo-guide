using Core.Execution.Domain.Outbox;
using Xunit;

namespace Core.Execution.Tests.Outbox;

public class OutboxRelayPolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    private static OutboxMessage Msg(int attempts = 0, DateTimeOffset? occurredAt = null, DateTimeOffset? publishedAt = null) =>
        new(Guid.NewGuid(), "core.eventos.v1", "{}", occurredAt ?? Now, publishedAt, attempts);

    [Fact]
    public void Mensagem_nova_esta_pronta_imediatamente()
    {
        Assert.Equal(Now, OutboxRelayPolicy.NextAttemptAt(Msg(attempts: 0)));
    }

    [Theory]
    [InlineData(1, 4)]     // 2^1 * 2s
    [InlineData(2, 8)]
    [InlineData(3, 16)]
    public void Backoff_e_exponencial(int attempts, int expectedSeconds)
    {
        var next = OutboxRelayPolicy.NextAttemptAt(Msg(attempts));
        Assert.Equal(Now + TimeSpan.FromSeconds(expectedSeconds), next);
    }

    [Fact]
    public void Backoff_tem_teto_de_cinco_minutos()
    {
        var next = OutboxRelayPolicy.NextAttemptAt(Msg(attempts: 30));
        Assert.Equal(Now + TimeSpan.FromMinutes(5), next);
    }

    [Fact]
    public void Lote_ignora_publicadas_e_ainda_nao_devidas()
    {
        var publicada = Msg(publishedAt: Now);
        var emBackoff = Msg(attempts: 5); // devida só em Now+64s
        var pronta = Msg();

        var batch = OutboxRelayPolicy.DueBatch([publicada, emBackoff, pronta], Now, batchSize: 10);

        Assert.Equal([pronta], batch);
    }

    [Fact]
    public void Lote_sai_em_ordem_de_ocorrencia_e_respeita_o_tamanho()
    {
        var antiga = Msg(occurredAt: Now - TimeSpan.FromMinutes(2));
        var media = Msg(occurredAt: Now - TimeSpan.FromMinutes(1));
        var nova = Msg(occurredAt: Now);

        var batch = OutboxRelayPolicy.DueBatch([nova, antiga, media], Now, batchSize: 2);

        Assert.Equal([antiga, media], batch);
    }
}
