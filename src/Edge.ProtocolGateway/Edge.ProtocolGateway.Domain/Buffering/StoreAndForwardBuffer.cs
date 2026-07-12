using Edge.ProtocolGateway.Domain.Translation;

namespace Edge.ProtocolGateway.Domain.Buffering;

public enum BufferPressure { Normal, High, Saturated }

/// <summary>
/// Store-and-forward da borda: se a WAN cair, o dado fica retido aqui até reconectar.
/// FIFO estrito (ordem de medição é sagrada pro TSDB) com marca d'alta:
///  - Normal: fluxo direto.
///  - High (>80%): sinal pro operador — WAN degradada há tempo demais.
///  - Saturated: NÃO descarta silenciosamente; recusa o enqueue e devolve o fato
///    pro chamador decidir (parar de ler o PLC é visível; perder dado calado não é).
/// A capacidade reflete o disco da borda; o estado real vai em arquivo na fase 1.
/// </summary>
public sealed class StoreAndForwardBuffer(int capacity)
{
    private readonly Queue<LineEvent> _queue = new();

    public int Count => _queue.Count;

    public BufferPressure Pressure =>
        _queue.Count >= capacity ? BufferPressure.Saturated
        : _queue.Count >= capacity * 0.8 ? BufferPressure.High
        : BufferPressure.Normal;

    public bool TryEnqueue(LineEvent evt)
    {
        if (_queue.Count >= capacity)
            return false;
        _queue.Enqueue(evt);
        return true;
    }

    /// <summary>Drena em ordem de chegada, no máximo <paramref name="max"/> por vez (lote do produtor Kafka).</summary>
    public IReadOnlyList<LineEvent> DrainBatch(int max)
    {
        var batch = new List<LineEvent>(Math.Min(max, _queue.Count));
        while (batch.Count < max && _queue.TryDequeue(out var evt))
            batch.Add(evt);
        return batch;
    }
}
