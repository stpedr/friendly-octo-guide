# Plano de execução das lacunas (propostas SMT/SMD)

**Planejamento — 2026-07-16. Sem código ainda: este doc é para revisão antes da
execução.** Complementa [`prontidao-propostas-smt.md`](./prontidao-propostas-smt.md):
lá está o *quanto* a plataforma está pronta; aqui está *o que fazer, em que ordem e
por quê* para receber as três pesquisas de mestrado.

## O que as dissertações fixaram (e que decide o plano)

- **Nenhum MES/SCADA específico** é nomeado nos três — falam "sistemas SCADA e MES"
  de forma genérica. → **conector MES genérico e adaptável** (decisão confirmada).
- **Victor** propõe um framework de **4 camadas** que mapeia 1:1 na plataforma:
  | Camada da dissertação (Victor) | Bloco da plataforma |
  |---|---|
  | 3.1.1.1 Coleta de dados | Edge + **conector MES** |
  | 3.1.1.2 Processamento (pré-proc + Random Forest) | `Predictive` |
  | 3.1.1.3 Base de conhecimento (Ishikawa) | `Knowledge` |
  | 3.1.1.4 Interface e assistente virtual | `Chatbot` + `Agents` (via Decision.Engine) |
- **Hallyson**: fonte são **imagens de alta resolução** de SPI/AOI + parâmetros;
  modelos **CNN / ViT / MambaVision / YOLOv5**, com **XAI** (explicabilidade) e
  classes desbalanceadas (augmentation).
- **Jeymerson**: **sistema especialista** com **inferência lógica** + base de
  conhecimento explícito/tácito + **XAI**, sobre MES.

**Consequência de projeto:** os três compartilham **coleta (MES) + causa raiz
(Ishikawa)**. Fazer isso uma vez, bem, destrava os três. Por isso a ordem abaixo é
**transversal primeiro**, depois as três "apps", depois treino e dados.

## Ordem de execução (e a dependência)

```
Épico 0  Contratos & modelo de dados     ─┐  (base de tudo)
Épico 1  Conector MES genérico           ─┼─ transversal → destrava os 3
Épico 2  Base de causa raiz (Ishikawa)   ─┘
                     │
        ┌────────────┼────────────┐
Épico 3 Victor    Épico 4 Hallyson   Épico 5 Jeymerson   (as 3 "apps")
        └────────────┼────────────┘
Épico 6  Treino/registro de modelo (Feast+MLflow, fase 2 da plataforma)
Épico 7  Dataset rotulado + validação no PIM (fora do software)
```

---

## Épico 0 — Contratos & modelo de dados

Schema primeiro (regra do repo). Três contratos novos em `schemas/`, todos com
`ativo_id` (liga na árvore ISA-95) e timestamp confiável (já temos `clock_source`).

- **`mes-evento.avsc`** — evento MES genérico: ordem, apontamento de produção,
  defeito, parada. Campos neutros (tipo, código, valor, ativo, turno) adaptáveis a
  qualquer MES.
- **`inspecao-smt.avsc`** — resultado de inspeção SPI/AOI: `board_id`, `ativo_id`,
  fonte (SPI/AOI), veredito, tipo de defeito, **referência à imagem no MinIO**
  (`image_ref`, não a imagem inline), parâmetros do processo.
- **`causa-raiz.avsc`** — nó de causa raiz no modelo Ishikawa: categoria 6M
  (Método/Máquina/Material/Mão de obra/Medição/Meio ambiente), sintoma, causa,
  ligação com `ativo_id` e com `motivo_codigo` do `parada-linha.avsc` **que já existe**.

**Entregável:** 3 `.avsc` + espelho no `Platform.Contracts` (codec) + testes de
roundtrip. **Risco:** baixo (padrão já dominado). **Bloqueia:** épicos 1, 2, 4.

---

## Épico 1 — Conector MES genérico (transversal)

Novo serviço `Mes.Connector` (Worker), ao lado do `Edge.ProtocolGateway`, mas para
o nível 3/4 (MES), não o chão de fábrica.

- **Domínio (puro, testável):** normalizador MES→`mes-evento.avsc`, cursor de
  polling idempotente (não reprocessa), e um `IMesAdapter` (porta) — a
  implementação genérica faz **poll REST/SQL**; o MES real entra trocando o adapter.
- **Worker:** poll → normaliza → valida (quality-gate-like) → Kafka `mes.eventos.v1`;
  malformado vai pra quarentena (mesma disciplina do ingest).
- **Deploy + doc.**

**Entregável:** serviço + testes CI-green + manifesto + doc. **Depende:** épico 0.
**Bloqueia:** épicos 3 e 5 (que consomem MES). **Pendência externa:** credencial/
endpoint do MES real (o adapter genérico roda contra um simulador até lá).

---

## Épico 2 — Base de causa raiz / Ishikawa (transversal)

No `Knowledge` (JSONB + pgvector). Reusa o que já existe: a árvore **ISA-95** (onde
a causa se pendura) e o **`parada-linha.avsc`** (motivo classificado = sintoma).

- **Domínio:** modelo Ishikawa 6M; ligação defeito/parada → categoria → causa raiz.
- **Persistência dupla:** estruturada (para inferência/regras) + vetorial (para RAG
  do chatbot).
- **Exposição:** GraphQL + **ferramenta MCP** para os agentes consultarem a base
  pelo mesmo contrato auditado.
- **Seed:** a partir dos `motivo_codigo` já previstos no OEE.

**Entregável:** domínio + store + testes + doc. **Depende:** épico 0. **Bloqueia:**
os três (todos usam causa raiz). **Pendência externa:** conhecimento tácito real da
linha (workshops de Ishikawa com a operação).

---

## Épico 3 — Victor: iDMSS com Random Forest

A mais aderente. As 4 camadas da dissertação já existem como blocos; falta o miolo
analítico e a costura.

- **`Predictive`:** caminho de **Random Forest** — vetor de features do MES/SCADA →
  inferência RF → ranking de causa raiz. Modelo **servido estático** (treino offline
  no épico 6).
- **`Agents` + `Chatbot`:** o **agente iDMSS** — consome `mes.eventos.v1` + saída do
  RF + base Ishikawa (épico 2) + observabilidade → propõe ação corretiva **pelos
  guardrails do `Decision.Engine`** (aprovação humana por criticidade). O chatbot é o
  "aplicativo" da dissertação.

**Entregável:** domínio RF + wiring do agente + testes + doc. **Depende:** épicos 1,
2. **Pendência externa:** dataset rotulado (épico 7) para treinar o RF.

---

## Épico 4 — Hallyson: visão computacional em SPI

Caminho novo de **imagem** (a telemetria escalar não serve).

- **Ingestão de imagem:** uploader/edge grava a imagem no **MinIO** e emite
  `inspecao-smt.avsc` com `image_ref` — a imagem **não** passa pelo codec escalar.
- **`Ai.Worker.Vision`:** contrato de inferência real; saída = defeito + **XAI**
  (explicação + confiança). Modelos candidatos documentados (**ViT / CNN /
  MambaVision / YOLOv5**), treino offline (épico 6).
- **Predictive/alerta:** defeito → alerta + ligação com Ishikawa (ex.: desgaste de
  estêncil = categoria *Máquina*).

**Entregável:** caminho de imagem + contrato de inferência + skeleton do worker +
doc. **Depende:** épicos 0, 2. **Pendência externa:** dataset de imagens SPI rotulado
+ o modelo treinado (épicos 6/7).

---

## Épico 5 — Jeymerson: gestão do conhecimento + inferência

Sistema especialista sobre a base Ishikawa, com explicabilidade.

- **`Knowledge`:** base explícito+tácito; **motor de inferência lógica** (regras
  interpretáveis) sobre o modelo Ishikawa — **complementa** o RAG do LLM, não o
  substitui (é o diferencial "sistema especialista + XAI" da dissertação).
- **`Agents`:** agente de gestão do conhecimento + relatório diário da linha.
- **Visibilidade/transparência:** já vem da espinha de observabilidade (acatech 3–4)
  — é reuso, não obra nova.

**Entregável:** motor de inferência + agente + doc. **Depende:** épico 2.
**Pendência externa:** elicitação do conhecimento tácito com a operação.

---

## Épico 6 — Treino e registro de modelo (fase 2 da plataforma)

- **Feast** (features) + **MLflow** (versionamento) para o RF (Victor) e a visão
  (Hallyson). Até aqui, modelos servidos estáticos; com isto, treino/rollout
  controlado (o Model Registry já está no diagrama).

**Depende:** épicos 3, 4. **Gatilho:** haver dataset e necessidade de re-treino.

---

## Épico 7 — Dataset rotulado + validação no PIM (fora do software)

- Dataset rotulado real (imagens SPI para Hallyson; eventos MES/falhas para Victor/
  Jeymerson) e validação em campo. **Não é engenharia de software** — é acesso a
  dados da linha e trabalho com a operação. É o pré-requisito que mais bloqueia
  resultado, e o que a plataforma **não** resolve sozinha.

---

## Resumo: o que a plataforma faz vs. o que depende de fora

| | Fazível no repo (código) | Depende de fora |
|---|---|---|
| Contratos, conector MES genérico, base Ishikawa | ✅ | endpoint MES real, conhecimento tácito |
| iDMSS (agente + Decision.Engine + Chatbot) | ✅ | dataset p/ treinar o RF |
| Caminho de imagem + worker de visão | ✅ (skeleton + contrato) | dataset SPI + modelo treinado |
| Inferência lógica / sistema especialista | ✅ | elicitação de regras com a operação |
| Feast/MLflow | ✅ (fase 2) | necessidade de re-treino contínuo |

**Veredito de planejamento:** tudo que é arquitetura/software está no nosso alcance
e cabe nas convenções do repo (schema-first, domínio puro, TDD gate). O que separa
"protótipo que roda" de "resultado de mestrado" é sobretudo **dado real rotulado** e
**acesso ao MES/à operação** — itens de campo, não de código.
