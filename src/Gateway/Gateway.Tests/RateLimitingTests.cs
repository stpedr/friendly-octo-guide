using Gateway.Domain;
using Xunit;

namespace Gateway.Tests;

public class RateLimitingTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Burst_esgota_e_regime_reabastece()
    {
        var limiter = new InMemoryRateLimiter(capacity: 2, refillPerSecond: 1);

        Assert.True(await limiter.TryTakeAsync("u1", T0));
        Assert.True(await limiter.TryTakeAsync("u1", T0));
        Assert.False(await limiter.TryTakeAsync("u1", T0));

        Assert.True(await limiter.TryTakeAsync("u1", T0.AddSeconds(1)));
    }

    [Fact]
    public async Task Chaves_diferentes_tem_buckets_independentes()
    {
        var limiter = new InMemoryRateLimiter(capacity: 1, refillPerSecond: 1);

        Assert.True(await limiter.TryTakeAsync("u1", T0));
        Assert.True(await limiter.TryTakeAsync("u2", T0));
        Assert.False(await limiter.TryTakeAsync("u1", T0));
    }

    [Fact]
    public async Task Teto_de_chaves_zera_o_estado_em_vez_de_crescer_sem_limite()
    {
        var limiter = new InMemoryRateLimiter(capacity: 1, refillPerSecond: 1, maxTrackedKeys: 2);

        Assert.True(await limiter.TryTakeAsync("a", T0));
        Assert.True(await limiter.TryTakeAsync("b", T0));

        // Terceira chave estoura o teto → estado zera → "a" volta com bucket cheio.
        Assert.True(await limiter.TryTakeAsync("c", T0));
        Assert.True(await limiter.TryTakeAsync("a", T0));
    }
}
