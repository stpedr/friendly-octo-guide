using Ai.Domain.Jobs;
using Xunit;

namespace Ai.Tests.Jobs;

public class DispatchPolicyTests
{
    private static AiJob Job(string modelType, int attempts = 0) =>
        new(Guid.NewGuid(), modelType, "{}", attempts);

    [Theory]
    [InlineData("llm", "ai.jobs.llm.v1")]
    [InlineData("vision", "ai.jobs.vision.v1")]
    [InlineData("embedding", "ai.jobs.embedding.v1")]
    [InlineData("LLM", "ai.jobs.llm.v1")] // tipo é indiferente a caixa
    public void Tipo_conhecido_encaminha_pro_topico_do_worker(string modelType, string expectedTopic)
    {
        var decision = DispatchPolicy.Decide(Job(modelType));
        Assert.Equal(RouteKind.Forward, decision.Kind);
        Assert.Equal(expectedTopic, decision.Topic);
    }

    [Fact]
    public void Tipo_desconhecido_vai_pra_DLQ_com_motivo()
    {
        var decision = DispatchPolicy.Decide(Job("clarividencia"));
        Assert.Equal(RouteKind.DeadLetter, decision.Kind);
        Assert.Equal(DispatchPolicy.DlqTopic, decision.Topic);
        Assert.Contains("clarividencia", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Tentativas_esgotadas_vao_pra_DLQ_mesmo_com_tipo_valido()
    {
        var decision = DispatchPolicy.Decide(Job("llm", attempts: DispatchPolicy.MaxAttempts));
        Assert.Equal(RouteKind.DeadLetter, decision.Kind);
    }

    [Fact]
    public void Ultima_tentativa_valida_ainda_encaminha()
    {
        var decision = DispatchPolicy.Decide(Job("llm", attempts: DispatchPolicy.MaxAttempts - 1));
        Assert.Equal(RouteKind.Forward, decision.Kind);
    }
}

public class IdempotencyLedgerTests
{
    private readonly IdempotencyLedger _ledger = new();
    private static readonly Guid JobId = Guid.NewGuid();

    [Fact]
    public void Primeira_reivindicacao_e_aceita()
    {
        Assert.Equal(JobClaim.Accepted, _ledger.TryClaim(JobId));
    }

    [Fact]
    public void Job_em_voo_nao_e_dado_a_outro_worker()
    {
        _ledger.TryClaim(JobId);
        Assert.Equal(JobClaim.InFlight, _ledger.TryClaim(JobId));
    }

    [Fact]
    public void Reprocesso_de_job_concluido_nunca_duplica_resultado()
    {
        _ledger.TryClaim(JobId);
        _ledger.Complete(JobId);
        Assert.Equal(JobClaim.AlreadyDone, _ledger.TryClaim(JobId));
    }

    [Fact]
    public void Release_devolve_job_em_voo_pra_fila()
    {
        _ledger.TryClaim(JobId);
        _ledger.Release(JobId);
        Assert.Equal(JobClaim.Accepted, _ledger.TryClaim(JobId));
    }

    [Fact]
    public void Release_nao_apaga_job_concluido()
    {
        _ledger.TryClaim(JobId);
        _ledger.Complete(JobId);
        _ledger.Release(JobId);
        Assert.Equal(JobClaim.AlreadyDone, _ledger.TryClaim(JobId));
    }
}
