using System.Diagnostics.Metrics;
using Gateway.Host;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace Gateway.IntegrationTests;

/// <summary>
/// Integração REAL do rate limiter distribuído contra um Redis/Valkey de verdade
/// (não fake): o script Lua roda no servidor, o token bucket vive no Redis, e o
/// fail-open é exercitado apontando pra um servidor morto. Prova que o limite vale
/// no agregado entre réplicas — a razão de existir do ValkeyRateLimiter.
///
/// Roda só com GATEWAY_VALKEY (ex.: "localhost:6379"); sem ela, auto-pula.
/// </summary>
public sealed class ValkeyRateLimiterLiveTests
{
    private static string? Endpoint => Environment.GetEnvironmentVariable("GATEWAY_VALKEY");
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

    private static ValkeyRateLimiter Limiter(IConnectionMultiplexer mux, double cap, double refill) =>
        new(mux, cap, refill, NullLogger<ValkeyRateLimiter>.Instance, new Meter("test"));

    [SkippableFact]
    public async Task Burst_esgota_e_regime_reabastece_num_redis_real()
    {
        Skip.If(Endpoint is null, "GATEWAY_VALKEY não definida — pulando teste de integração.");
        await using var mux = await ConnectionMultiplexer.ConnectAsync($"{Endpoint},abortConnect=false");
        await mux.GetDatabase().KeyDeleteAsync($"rl:live-{nameof(Burst_esgota_e_regime_reabastece_num_redis_real)}");

        var key = $"live-{Guid.NewGuid()}";
        var limiter = Limiter(mux, cap: 2, refill: 1);

        Assert.True(await limiter.TryTakeAsync(key, T0));            // burst 1
        Assert.True(await limiter.TryTakeAsync(key, T0));            // burst 2
        Assert.False(await limiter.TryTakeAsync(key, T0));           // esgotou
        Assert.True(await limiter.TryTakeAsync(key, T0.AddSeconds(1))); // 1s → +1 token
    }

    [SkippableFact]
    public async Task Estado_e_compartilhado_entre_instancias_do_limiter()
    {
        Skip.If(Endpoint is null, "GATEWAY_VALKEY não definida — pulando teste de integração.");
        await using var mux = await ConnectionMultiplexer.ConnectAsync($"{Endpoint},abortConnect=false");

        var key = $"live-{Guid.NewGuid()}";
        // Duas instâncias distintas = duas réplicas do Gateway compartilhando o Redis.
        var replicaA = Limiter(mux, cap: 1, refill: 0.001);
        var replicaB = Limiter(mux, cap: 1, refill: 0.001);

        Assert.True(await replicaA.TryTakeAsync(key, T0));   // réplica A gasta o único token
        Assert.False(await replicaB.TryTakeAsync(key, T0));  // réplica B vê o bucket vazio — limite é agregado
    }

    [SkippableFact]
    public async Task Valkey_fora_do_ar_degrada_pro_limitador_local_fail_open()
    {
        Skip.If(Endpoint is null, "GATEWAY_VALKEY não definida — pulando teste de integração.");
        // Porta morta: connect não aborta, mas cada comando falha → fail-open pro in-memory.
        await using var dead = await ConnectionMultiplexer.ConnectAsync("localhost:6390,abortConnect=false,connectTimeout=300,syncTimeout=300");
        var limiter = Limiter(dead, cap: 1, refill: 1);

        // Mesmo com o Redis fora, a borda não derruba tráfego legítimo: o take passa.
        Assert.True(await limiter.TryTakeAsync($"live-{Guid.NewGuid()}", T0));
    }
}
