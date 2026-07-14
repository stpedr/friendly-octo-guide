using System.Diagnostics.Metrics;
using Gateway.Domain;
using StackExchange.Redis;

namespace Gateway.Host;

/// <summary>
/// Token bucket distribuído no Valkey: o script Lua lê, reabastece e decide numa
/// operação só — atômico entre réplicas do Gateway, sem corrida. Se o Valkey cair,
/// a borda degrada pro limitador local (fail-open controlado): disponibilidade da
/// borda vale mais que precisão do limite agregado.
/// </summary>
public sealed partial class ValkeyRateLimiter : IRateLimiter
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Valkey indisponível; rate limit degradado pro limitador local")]
    private static partial void LogDegraded(ILogger logger, Exception ex);

    // ARGV: capacidade, reabastecimento/s, agora em ms. Estado por chave: hash {tokens, ts}.
    // O TTL é 2x o tempo de reencher o bucket — chave parada some sozinha.
    private const string Script = """
        local data = redis.call('HMGET', KEYS[1], 'tokens', 'ts')
        local capacity = tonumber(ARGV[1])
        local refill = tonumber(ARGV[2])
        local now = tonumber(ARGV[3])
        local tokens = tonumber(data[1])
        local ts = tonumber(data[2])
        if tokens == nil then
          tokens = capacity
          ts = now
        end
        local elapsed = math.max(0, now - ts) / 1000.0
        tokens = math.min(capacity, tokens + elapsed * refill)
        local allowed = 0
        if tokens >= 1 then
          tokens = tokens - 1
          allowed = 1
        end
        redis.call('HSET', KEYS[1], 'tokens', tokens, 'ts', now)
        redis.call('PEXPIRE', KEYS[1], math.ceil(capacity / refill * 2000))
        return allowed
        """;

    private readonly IConnectionMultiplexer _valkey;
    private readonly InMemoryRateLimiter _fallback;
    private readonly ILogger<ValkeyRateLimiter> _log;
    private readonly Counter<long> _degraded;
    private readonly double _capacity;
    private readonly double _refillPerSecond;

    public ValkeyRateLimiter(
        IConnectionMultiplexer valkey,
        double capacity,
        double refillPerSecond,
        ILogger<ValkeyRateLimiter> log,
        Meter meter)
    {
        _valkey = valkey;
        _capacity = capacity;
        _refillPerSecond = refillPerSecond;
        _fallback = new InMemoryRateLimiter(capacity, refillPerSecond);
        _log = log;
        _degraded = meter.CreateCounter<long>("gateway.ratelimit.degraded");
    }

    public async ValueTask<bool> TryTakeAsync(string key, DateTimeOffset now, CancellationToken ct = default)
    {
        try
        {
            var result = await _valkey.GetDatabase().ScriptEvaluateAsync(
                Script,
                [new RedisKey($"rl:{key}")],
                [_capacity, _refillPerSecond, now.ToUnixTimeMilliseconds()]);
            return (long)result == 1;
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            _degraded.Add(1);
            LogDegraded(_log, ex);
            return await _fallback.TryTakeAsync(key, now, ct);
        }
    }
}
