using Platform.Contracts;
using Xunit;

namespace Mes.Connector.Tests;

public class MesEventoCodecTests
{
    [Fact]
    public void Roundtrip_preserva_todos_os_campos()
    {
        var original = new MesEventoRecord(
            EventId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            AtivoId: "envase.linha2.enchedora",
            Tipo: TipoMesEvento.Defeito,
            Codigo: "SOLDER-BRIDGE",
            Quantidade: 2.5,
            Texto: "ponte de solda",
            Turno: "dia",
            SistemaOrigem: "simulador",
            OccurredAt: DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_123),
            ClockSource: 1);

        var decoded = MesEventoCodec.Decode(MesEventoCodec.Encode(original));

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Campos_opcionais_nulos_sobrevivem()
    {
        var original = new MesEventoRecord(
            Guid.NewGuid(), "ativo", TipoMesEvento.Parada, "MANUT",
            Quantidade: null, Texto: null, Turno: null, SistemaOrigem: "mes-x",
            OccurredAt: DateTimeOffset.UnixEpoch);

        Assert.Equal(original, MesEventoCodec.Decode(MesEventoCodec.Encode(original)));
    }

    [Fact]
    public void Tipo_e_serializado_como_texto_legivel()
    {
        var json = MesEventoCodec.Encode(new MesEventoRecord(
            Guid.NewGuid(), "ativo", TipoMesEvento.Apontamento, "OK",
            null, null, null, "mes-x", DateTimeOffset.UnixEpoch));

        Assert.Contains("Apontamento", json);
    }

    [Fact]
    public void Json_vazio_lanca()
    {
        Assert.Throws<System.ArgumentException>(() => MesEventoCodec.Decode(""));
    }
}
