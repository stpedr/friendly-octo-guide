using System.Text.Json;
using Confluent.Kafka;
using Mes.Connector.Domain;
using Platform.Contracts;

namespace Mes.Connector.Worker;

/// <summary>
/// Destinos do evento MES: tópico principal (aceitos) e quarentena (rejeitados, com o
/// motivo + linha crua intacta pra replay). Mapeia domínio→contrato e serializa JSON.
/// </summary>
public sealed class MesEventSink : IDisposable
{
    private readonly MesOptions _options;
    private readonly IProducer<string, string> _producer;

    public MesEventSink(MesOptions options)
    {
        _options = options;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = options.KafkaBootstrap,
            EnableIdempotence = true,
        }).Build();
    }

    public async Task PublishAsync(MesEvent evento, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evento);

        var record = new MesEventoRecord(
            evento.EventId, evento.AtivoId, (TipoMesEvento)(int)evento.Tipo, evento.Codigo,
            evento.Quantidade, evento.Texto, evento.Turno, evento.SistemaOrigem, evento.OccurredAt);

        await _producer.ProduceAsync(_options.EventTopic,
            new Message<string, string> { Key = evento.EventId.ToString(), Value = MesEventoCodec.Encode(record) },
            ct);
    }

    public async Task QuarantineAsync(RawMesRow row, string reason, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(row);

        var body = JsonSerializer.Serialize(new { reason, row });
        await _producer.ProduceAsync(_options.QuarantineTopic,
            new Message<string, string> { Key = row.Cursor, Value = body },
            ct);
    }

    public void Dispose() => _producer.Dispose();
}
