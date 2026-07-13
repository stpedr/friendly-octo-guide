using System.Collections.Concurrent;

namespace Gateway.Domain;

/// <summary>
/// Contrato do rate limit da borda. A chave identifica o chamador (sub do token ou IP);
/// o relógio entra por parâmetro pra decisão ser determinística em teste.
/// </summary>
public interface IRateLimiter
{
    ValueTask<bool> TryTakeAsync(string key, DateTimeOffset now, CancellationToken ct = default);
}

/// <summary>
/// Implementação local por réplica: um TokenBucket por chamador. Vale sozinha em
/// instância única e serve de fallback quando o Valkey está fora — a borda degrada
/// pra limite por réplica em vez de derrubar tráfego legítimo.
/// </summary>
public sealed class InMemoryRateLimiter(double capacity, double refillPerSecond, int maxTrackedKeys = 100_000)
    : IRateLimiter
{
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();

    public ValueTask<bool> TryTakeAsync(string key, DateTimeOffset now, CancellationToken ct = default)
    {
        // Teto de chaves rastreadas: sob um flood de IPs forjados o dicionário não cresce
        // sem limite — descarta o estado e recomeça (os buckets renascem cheios, o que é
        // aceitável: o limite por chave continua valendo dali em diante).
        if (_buckets.Count >= maxTrackedKeys && !_buckets.ContainsKey(key))
            _buckets.Clear();

        var bucket = _buckets.GetOrAdd(key, _ => new TokenBucket(capacity, refillPerSecond));
        return ValueTask.FromResult(bucket.TryTake(now));
    }
}
