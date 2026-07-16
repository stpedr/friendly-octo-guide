# Camada de OEE e KPI de manufatura

**Status: definido em 2026-07 — OEE como métrica derivada no TSDB, calculada
sobre telemetria + ordens + paradas.**

## Por que isso é governança, não feature

É uma plataforma de **linha de produção**, mas a TSDB só tinha métrica bruta
(valor de sensor, latência de serviço). Faltava a definição do que é
**produzir bem**. Sem OEE, o estágio 3 do acatech ("o que está acontecendo?")
fica técnico e não industrial: o Grafana mostra saúde de container, não saúde
de linha. Esta camada fecha essa lacuna com a métrica-padrão da indústria.

## O que é OEE

**OEE = Disponibilidade × Performance × Qualidade** — um número de 0 a 1 por
ativo produtivo, e as três componentes por trás dele.

| Componente | Fórmula | Vem de |
|---|---|---|
| **Disponibilidade** | tempo operando ÷ tempo planejado | `schemas/parada-linha.avsc` (paradas não planejadas) |
| **Performance** | produção real ÷ produção teórica no tempo operando | telemetria (contagem) vs. taxa nominal do ativo |
| **Qualidade** | peças boas ÷ peças totais | telemetria de refugo + apontamento |

As **seis grandes perdas** clássicas mapeiam direto no enum `CategoriaParada`
do schema de parada: falha e setup (disponibilidade), pequenas paradas e
velocidade reduzida (performance), refugo e retrabalho (qualidade).

## Onde é calculado (o fluxo)

Nada de banco novo — reusa a espinha que já existe:

```
telemetria (Kafka)  ─┐
paradas   (Kafka)   ─┼─► Predictive.Worker (janela por ativo) ─► OEE como métrica ─► VictoriaMetrics ─► Grafana
ordens (Core.Exec)  ─┘        (mesmo padrão do scoring EWMA/z-score)                  (TSDB já existente)      (painel de linha)
```

- O **motor de janela** é o mesmo lugar do scoring online (`src/Predictive/`):
  ele já consome o stream por ativo. OEE é uma agregação por janela (turno,
  hora, ordem) ao lado da detecção de anomalia — não um serviço novo.
- O resultado sai como **métrica** (`oee_disponibilidade`, `oee_performance`,
  `oee_qualidade`, `oee` — labels `ativo_id`, `planta`, `linha`, `turno`) no
  **VictoriaMetrics**, o TSDB que a arquitetura já opera. Um banco a menos.
- O **roll-up** usa a [árvore de ativos](./modelo-de-ativos-isa95.md): OEE de
  `WorkUnit` agrega pra `Line`, `Area`, `Site`. É a árvore que torna
  "OEE da planta" uma soma bem definida, não um chute.

## O contrato novo

`schemas/parada-linha.avsc` (`linha.oee.v1`) é o que faltava: **motivo de
parada classificado**. Telemetria sozinha diz *que* parou; o motivo diz *por
quê* — e é o motivo que vira Pareto acionável ("40% da perda de disponibilidade
da linha 2 é `ENCH-JAM-02`"). A parada é aberta na queda (detecção automática
por telemetria, ou apontamento do operador na tela) e fechada na volta.

Performance e Qualidade dependem de **contagem de peças** e **refugo**, que hoje
chegam como telemetria genérica; a formalização desses dois sinais (tag padrão
de contador por ativo) é trabalho de integração com cada linha, não de schema.

## Painéis e alertas (reuso)

- **Grafana**: um painel "OEE por linha/turno" ao lado do painel de saúde —
  mesma stack, mesma regra de um-core-três-shells do cliente de observabilidade.
- **Alertas**: queda de OEE abaixo do alvo entra no mesmo `Alertmanager → ntfy`
  com runbook, como qualquer outro alerta. OEE baixo com motivo `Falha`
  recorrente é exatamente o gatilho que a camada 5 (Predictive) quer antecipar.

## O que falta pra fase 2

- Tag padronizada de **contador de peças** e **refugo** por ativo (hoje:
  telemetria genérica; sem isso, só Disponibilidade é 100% automática).
- Catálogo de `motivo_codigo` por planta (master data, ao lado dos ativos).
- Alvo de OEE por ativo (o "99,5%" do OEE) declarado como o SLO industrial —
  candidato natural a entrar no Pyrra junto dos SLOs de serviço.
