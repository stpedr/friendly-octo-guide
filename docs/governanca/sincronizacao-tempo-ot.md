# Sincronização de tempo na borda (OT)

**Status: definido em 2026-07 — NTP (chrony) como baseline na borda, fonte de
relógio declarada no contrato da leitura, quality gate podendo rejeitar relógio
não confiável.**

## Por que isso é governança

Vários mecanismos da plataforma pressupõem **relógios sincronizados** na camada
OT, mas a sincronização não era infraestrutura declarada em lugar nenhum:

- o **quality gate** valida *clock drift* (`MeasuredAt - ReceivedAt`) — precisa de
  uma referência confiável pra saber o que é drift;
- o **trace-id ponta a ponta** correlaciona eventos edge↔nuvem por timestamp;
- o **Predictive** faz z-score/EWMA sobre janelas — janela mal alinhada = falso
  positivo/negativo;
- o **OEE** soma durações de parada — relógio torto corrompe a Disponibilidade.

Em OT, deriva de relógio entre PLCs/sensores é comum e **silenciosa**: o dado
chega com integridade perfeita e timestamp errado. Drift sozinho não pega isso —
um relógio confiantemente errado pode cair dentro da janela de drift por acaso.

## O que foi implementado

### 1. NTP na borda (chrony)

`deploy/edge-protocol-gateway/chrony.conf`: baseline de NTP em todo nó de borda,
com `makestep` pra corrigir relógio grosseiramente errado no boot e `rtcsync` pra
não derivar entre reboots. O nó de borda pode servir como **stratum local** pra
rede OT isolada (níveis 0–2 do Purdue não alcançam a internet). O offset e o
stratum viram log/métrica — o drift real do nó é observável, não suposto.

**PTP (IEEE 1588)** entra onde a precisão sub-milissegundo importar (correlação
fina de eventos de linha) — depende de suporte de hardware nos switches/NICs, é
decisão de fase 2.

### 2. Fonte de relógio no contrato da leitura

`schemas/sensor-reading.avsc` ganhou `clock_source` (BACKWARD, default 0):

| valor | significado |
|---|---|
| 0 | `Unknown` — não declarado, tratado como não confiável |
| 1 | `Ntp` — sincronizado por NTP (chrony) |
| 2 | `Ptp` — sincronizado por PTP (sub-ms) |
| 3 | `Unsynced` — o dispositivo declarou que NÃO está sincronizado |

O codec (`SensorReadingCodec`) carrega o campo; payload antigo sem ele decodifica
como `Unknown` (BACKWARD provado em teste). O domínio mapeia via `ClockSourceMap`.

### 3. Regra no quality gate

`QualityGate` ganhou `requireSyncedClock` (**off por padrão**): quando ligado,
rejeita leitura de relógio `Unsynced` ou `Unknown` com o motivo
`UnsyncedClock` — **antes** das checagens de drift/staleness, que perdem o sentido
sobre um timestamp não confiável. Rejeitada vai pra quarentena, como qualquer
outra (nunca descartada).

O drift máximo continua parametrizado (`maxClockDrift`) e testado.

## Rollout (por que `requireSyncedClock` nasce off)

Ligar a exigência antes do edge popular `clock_source` rejeitaria **tudo** (todo
payload chegaria como `Unknown`). A ordem correta:

1. Provisionar chrony nos nós de borda (`chrony.conf`).
2. O `Edge.ProtocolGateway` passa a declarar `clock_source` no encode, a partir do
   próprio estado de sincronização (chrony `tracking`). **← passo pendente**
3. Só então ligar `requireSyncedClock` no ingest, planta por planta.

## O que falta

- Passo 2 do rollout: o edge declarar `clock_source` (hoje encoda `Unknown`).
- `chrony.conf` no compose/Pi (`deploy/raspberry/`) além do nó de borda K8s.
- PTP onde a precisão exigir (fase 2, depende de hardware).
