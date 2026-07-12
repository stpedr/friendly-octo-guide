using System.Text.Json;
using Edge.ProtocolGateway.Domain.Buffering;
using Edge.ProtocolGateway.Domain.Translation;
using MQTTnet;
using MQTTnet.Client;
using Platform.ServiceDefaults;

namespace Edge.ProtocolGateway.Worker;

/// <summary>
/// Lado OT: assina linha/+/sensor/+ no broker MQTT local e empilha no buffer.
/// Payload dos sensores: {"value": 812.5, "measuredAt": "..."} — o sensor_id vem do tópico.
/// Em prod a conexão é mTLS com certificado X.509 individual por dispositivo.
/// </summary>
public sealed partial class MqttIngress(
    StoreAndForwardBuffer buffer,
    ServiceInstrumentation instrumentation,
    IConfiguration config,
    ILogger<MqttIngress> log) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(config["Mqtt:Host"] ?? "localhost", config.GetValue("Mqtt:Port", 1883))
            .WithClientId("edge-protocol-gateway")
            .Build();

        var received = instrumentation.Meter.CreateCounter<long>("edge.readings.received");
        var refused = instrumentation.Meter.CreateCounter<long>("edge.buffer.refused");

        client.ApplicationMessageReceivedAsync += args =>
        {
            var sensorId = ProtocolTranslator.SensorIdFromTopic(args.ApplicationMessage.Topic);
            if (string.IsNullOrEmpty(sensorId))
                return Task.CompletedTask; // tópico fora do padrão da linha não entra

            var body = JsonSerializer.Deserialize<SensorPayload>(args.ApplicationMessage.PayloadSegment, JsonOpts);
            if (body is null)
                return Task.CompletedTask;

            received.Add(1);
            var evt = new LineEvent(sensorId, body.Value, body.MeasuredAt ?? DateTimeOffset.UtcNow);
            if (!buffer.TryEnqueue(evt))
            {
                // Nunca descarta calado: saturação vira métrica + log — e o KafkaEgress
                // é quem tem que se explicar (WAN caída ou consumidor lento).
                refused.Add(1);
                LogSaturated(buffer.Count);
            }
            return Task.CompletedTask;
        };

        // Reconexão com retry — broker local pode reiniciar; o gateway não morre junto.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!client.IsConnected)
                {
                    await client.ConnectAsync(options, stoppingToken);
                    await client.SubscribeAsync(
                        factory.CreateSubscribeOptionsBuilder()
                            .WithTopicFilter("linha/+/sensor/+")
                            .Build(),
                        stoppingToken);
                    LogConnected(options.ChannelOptions.ToString() ?? "mqtt");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogReconnecting(ex);
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private sealed record SensorPayload(double Value, DateTimeOffset? MeasuredAt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Conectado ao broker MQTT: {Endpoint}")]
    private partial void LogConnected(string endpoint);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reconectando ao broker MQTT após falha")]
    private partial void LogReconnecting(Exception ex);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Buffer da borda SATURADO ({Count} eventos) — leitura recusada, verificar WAN/Kafka")]
    private partial void LogSaturated(int count);
}
