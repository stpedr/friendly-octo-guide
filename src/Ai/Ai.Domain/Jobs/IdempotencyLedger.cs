namespace Ai.Domain.Jobs;

public enum JobClaim
{
    Accepted,      // primeira vez: processe
    InFlight,      // outro worker está nele agora — não duplique
    AlreadyDone,   // resultado já existe — reprocesso NUNCA duplica resultado
}

/// <summary>
/// Idempotência por job-id: o Kafka entrega at-least-once, então o worker precisa
/// deduplicar. Semântica pura aqui; o estado real vai pro Valkey (SET NX + TTL)
/// na fase 1 pra valer entre réplicas.
/// </summary>
public sealed class IdempotencyLedger
{
    private enum State { InFlight, Done }
    private readonly Dictionary<Guid, State> _jobs = [];

    public JobClaim TryClaim(Guid jobId) => _jobs.TryGetValue(jobId, out var state)
        ? state == State.Done ? JobClaim.AlreadyDone : JobClaim.InFlight
        : Claim(jobId);

    public void Complete(Guid jobId) => _jobs[jobId] = State.Done;

    /// <summary>Worker morreu no meio: libera o job pra outro tentar.</summary>
    public void Release(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var state) && state == State.InFlight)
            _jobs.Remove(jobId);
    }

    private JobClaim Claim(Guid jobId)
    {
        _jobs[jobId] = State.InFlight;
        return JobClaim.Accepted;
    }
}
