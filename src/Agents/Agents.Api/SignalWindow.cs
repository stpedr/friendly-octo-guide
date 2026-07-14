using System.Collections.Concurrent;
using Agents.Domain.Diagnosis;

namespace Agents.Api;

/// <summary>
/// Janela deslizante de sinais em memória que o agente correlaciona. Alimentada
/// pelo consumo de alertas/telemetria; poda o que passou da retenção pra não
/// crescer sem limite. Fase 1: materializar do Big Data Pool/TSDB pra correlação
/// histórica além da janela quente.
/// </summary>
public sealed class SignalWindow(TimeSpan retention)
{
    private readonly ConcurrentQueue<Signal> _signals = new();

    public void Add(Signal signal, DateTimeOffset now)
    {
        _signals.Enqueue(signal);
        while (_signals.TryPeek(out var oldest) && now - oldest.At > retention)
            _signals.TryDequeue(out _);
    }

    public IReadOnlyList<Signal> Snapshot() => [.. _signals];
}
