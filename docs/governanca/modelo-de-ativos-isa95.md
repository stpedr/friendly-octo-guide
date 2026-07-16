# Modelo de ativos (ISA-95): decisão registrada (ADR)

**Status: decidido em 2026-07 — hierarquia ISA-95 explícita, multi-site por
árvore, isolamento por atributo ABAC (sem `tenant_id`).**

## Contexto

A arquitetura tratava `planta`/`linha`/`turno` só como **atributos de RBAC**.
Faltava o modelo canônico de **onde as coisas estão fisicamente**: qual site,
qual área, qual máquina, qual componente. Sem ele três coisas ficam no ar:

- a telemetria do edge não tem onde se pendurar semanticamente (um `sensor_id`
  solto não diz de qual máquina veio);
- o chatbot ("por que a **linha 2** parou às 14h?") depende de um grafo de
  ativos pra correlacionar telemetria, ordem e parada;
- multi-site (a decisão desta ADR) precisa de uma raiz comum pra navegar.

O padrão de fato pra isso na indústria é o **modelo de equipamento da ISA-95 /
IEC 62264**. Adotamos os níveis dela em vez de inventar hierarquia própria.

## Decisão

### 1. A hierarquia é a da ISA-95

```
Enterprise            empresa (raiz — uma por instância)
└─ Site               planta / unidade geográfica        ← attr:planta
   └─ Area            setor (envase, montagem, utilidades)
      └─ Line         linha de produção                   ← attr:linha
         └─ WorkCell  célula / estação
            └─ WorkUnit  máquina / equipamento individual
```

Um **sensor/PLC não é um nível**: ele se pendura num `WorkUnit` (ou `WorkCell`)
pelo prefixo do `sensor_id`, que passa a carregar o `ativo_id` do nó
(`envase.spitau.linha2.enchedora.motor01`). Assim `SensorReading.sensor_id`
(já existente em `schemas/sensor-reading.avsc`) **ganha significado sem mudar de
schema** — o prefixo resolve a árvore.

### 2. Multi-site, mas ainda single-tenant

A [ADR de multi-tenant](./multi-tenant.md) decidiu **single-tenant por
instância**. Esta ADR **não a contradiz**: multi-site aqui é **várias plantas de
uma mesma empresa** na mesma instância — vários nós `Site` sob o único
`Enterprise` raiz. Continua valendo:

- **nenhum `tenant_id`** em tabela, tópico ou índice;
- o isolamento entre plantas é o `attr:planta` do JWT (`Platform.AccessControl`),
  agora **espelhado no campo `planta` do nó de ativo** — o RBAC filtra pelo
  atributo sem precisar percorrer a árvore, e a árvore existe pra navegação
  e correlação.

Se um dia for SaaS multi-empresa, a raiz deixa de ser única — isso reabre a ADR
de multi-tenant, não esta.

### 3. É master data, não telemetria

O contrato novo é `schemas/ativo.avsc` (`linha.ativos.v1`): um nó por registro,
encadeado pelo pai. É **master data** — muda por cadastro/manutenção, não por
leitura de sensor. A **fonte da verdade** é o Postgres (relacional
transacional, como as ordens); o tópico Kafka `linha.ativos` publica **mudanças**
(outbox pattern, igual `Core.Execution`) pra que edge, Predictive, Knowledge e a
linhagem do lake consumam a árvore atual sem chamar o serviço de volta.

## Consequências

- **Onde vive o CRUD**: cadastro/edição de ativos é uma responsabilidade de
  domínio. Cabe em `Core.Execution` (já é o dono de master data de produção) ou
  num serviço `Assets` dedicado — decisão de implementação, não de arquitetura.
  Até lá, o modelo é **seed** (um YAML/SQL de bootstrap por planta).
- **Edge**: o enrollment de um sensor novo (já previsto no `Edge.ProtocolGateway`)
  passa a **exigir** um `ativo_id` de destino válido — sensor sem ativo é
  rejeitado no mesmo espírito do quality gate ("não preserva lixo com
  integridade perfeita").
- **Chatbot/Knowledge**: o RAG ganha a árvore como contexto estruturado; o
  filtro RBAC por `planta`/`linha` continua o mesmo mecanismo.
- **OEE**: a [camada de OEE](./oee-kpi-manufatura.md) agrega KPI **por nó da
  árvore** (OEE de máquina → célula → linha → site). É a árvore que torna o
  roll-up possível.
- **Linhagem**: cada registro no lake já carrega dispositivo de origem; com a
  árvore, o `ativo_id` entra na linhagem (OpenLineage) — auditável até a máquina.

## O que falta pra fase 2

- Serviço/rota de CRUD de ativos com outbox (hoje: seed).
- Validação de `ativo_id` no enrollment do edge.
- Versionamento temporal do nó (uma máquina realocada de linha muda de pai —
  precisa de histórico pra não reescrever o passado do OEE).
