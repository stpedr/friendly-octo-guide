using Edge.ProtocolGateway.Domain.Buffering;
using Edge.ProtocolGateway.Domain.Translation;
using Edge.ProtocolGateway.Worker;
using Platform.ServiceDefaults;

// Path próprio da fábrica pra nuvem, independente do Gateway web:
// MQTT (broker local da DMZ industrial) → tradução → store-and-forward → Kafka.
// Fluxo default unidirecional OT→IT (modelo Purdue, nível 3.5).

var instrumentation = new ServiceInstrumentation("edge-protocol-gateway");

var builder = Host.CreateApplicationBuilder(args);
builder.AddPlatformDefaults(instrumentation);

builder.Services.AddSingleton(instrumentation);
builder.Services.AddSingleton(new StoreAndForwardBuffer(
    capacity: builder.Configuration.GetValue("Edge:BufferCapacity", 100_000)));
builder.Services.AddSingleton(new ProtocolTranslator([
    // Mapa da linha — fase 1 carrega do cadastro de dispositivos (com cert X.509 por sensor).
    new RegisterMapping("temp-forno-01", Address: 100, Scale: 0.1, Offset: -40),
    new RegisterMapping("pressao-linha-02", Address: 101, Scale: 0.01, Offset: 0),
]));
builder.Services.AddHostedService<MqttIngress>();
builder.Services.AddHostedService<KafkaEgress>();

await builder.Build().RunAsync();
