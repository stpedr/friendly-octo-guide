# Estado da execução das propostas SMT/SMD — pronto vs. a implementar

**Handoff — 2026-07-16.** Onde a execução parou e o que falta, com detalhe pra
retomar e **validar depois** (a pedido: implementar num segundo momento). Complementa
o [plano](./plano-execucao-propostas-smt.md) e a [prontidão](./prontidao-propostas-smt.md).

## Leia isto primeiro: "verde no CI" ≠ "validado rodando"

Tudo abaixo marcado ✅ passou no **CI** (build + testes unitários + cobertura). Mas o
CI **não executa** os serviços — não sobe Kafka, Postgres, MinIO, GPU nem MES. Então:

- **Domínio puro** (classificadores, normalizadores, ranking): ✅ = testado de verdade.
- **IO** (workers, stores, agentes): quando existir, "verde" = **compila**, não = roda.
  A validação de runtime precisa do ambiente real (compose/cluster + dado).

Por isso o que resta foi **deixado documentado** em vez de implementado às cegas: são
esqueletos de IO e integrações que só valem a pena com o ambiente pra validar.

## O que está pronto e verde (não refazer)

| Épico | Entregue | Onde |
|---|---|---|
| **#6 Conector MES** | serviço completo: coleta genérica → `mes.eventos.v1`, normalização, quarentena, idempotência, simulador | `src/Mes.Connector/`, `schemas/mes-evento.avsc`, `Platform.Contracts/MesEventoCodec` |
| **#7 Ishikawa** | classificador 6M, diagrama espinha-de-peixe, seed de causa raiz | `src/Knowledge/Knowledge.Domain/Ishikawa/`, `schemas/causa-raiz.avsc` |
| **#8 iDMSS (Victor)** | ranking de diagnóstico explicável (XAI) | `src/Predictive/Predictive.Domain/Diagnosis/`, `docs/idmss-victor.md` |

Commits: `267fffb` (MES), `6e3f373` (Ishikawa), `f7d0582` (iDMSS + seed).

## O que falta, por épico (para implementar e validar depois)

### #6 Conector MES — adapter real + cursor persistente
- **`RestMesAdapter` / `SqlMesAdapter`** implementando `IMesAdapter` (hoje só o
  `SimulatorMesAdapter`). Poll do MES real (REST ou SQL), mapeando pra `RawMesRow`.
- **Persistir o cursor** (hoje em memória → re-poll no restart). Uma tabela Postgres
  ou chave no Valkey.
- **Validar**: subir compose (Kafka), rodar o worker, ver evento em `mes.eventos.v1`.
- **Bloqueio externo**: endpoint/credencial do MES real.

### #7 Ishikawa — persistência + exposição (2º push)
- **`KnowledgeStore`**: tabela `causa_raiz` (estruturada, p/ inferência) + índice
  **pgvector** (p/ RAG). Upsert + consulta por `ativo_id`/categoria.
- **GraphQL** (HotChocolate) e **ferramenta MCP** pros agentes consultarem pelo
  contrato auditado.
- **Seed real**: ligar `CausaRaizSeed` aos `motivo_codigo` do OEE no boot.
- **Validar**: pgvector no compose, semear, consultar por ativo.
- **Bloqueio externo**: conhecimento tácito (workshops de Ishikawa com a operação).

### #8 iDMSS Victor — camada de interface (agente)
- **Agente iDMSS**: costurar `Chatbot` (interface) + `Decision.Engine` (guardrails,
  aprovação por criticidade) + `Agents`, consumindo `mes.eventos.v1` + base Ishikawa
  + `DiagnosisRanking`. Ferramenta MCP por operação.
- **Random Forest servido**: hoje o ranking usa peso 1.0 (frequência); o RF entra
  como peso. Servir modelo estático (worker) → treino no #11.
- **Validar**: conversa "por que a linha 2 parou?" retornando causa ranqueada + ação.
- **Bloqueio externo**: dataset rotulado de falhas do PIM p/ treinar o RF.

### #9 Hallyson — visão em SPI (caminho de imagem)
- **`schemas/inspecao-smt.avsc`**: resultado de inspeção com `image_ref` (ponteiro
  MinIO, imagem não inline), veredito, tipo de defeito, parâmetros.
- **Ingestão de imagem**: uploader/edge grava no MinIO + emite o evento.
- **`Ai.Worker.Vision`**: contrato de inferência (defeito + **XAI**: explicação +
  confiança). Modelo servido estático — candidatos: ViT / CNN / MambaVision / YOLOv5.
- **Ligação Ishikawa**: defeito → categoria (ex.: desgaste de estêncil = *Máquina*).
- **Validar**: precisa de GPU + modelo + imagens.
- **Bloqueio externo**: dataset SPI rotulado + modelo treinado.

### #10 Jeymerson — inferência lógica (sistema especialista)
- **Motor de inferência** sobre a base Ishikawa: regras interpretáveis (XAI),
  complementando o RAG do LLM. A semente já existe (`IshikawaClassifier` é regra por
  palavra-chave); estender pra encadeamento de regras causa→efeito.
- **Agente de conhecimento** + relatório diário da linha.
- **Visibilidade/transparência**: reuso da observabilidade (acatech 3–4), sem obra nova.
- **Bloqueio externo**: elicitação das regras com a operação.

### #11 Treino/registro (fase 2 da plataforma)
- **Feast** (features) + **MLflow** (versionamento) p/ o RF (#8) e a visão (#9).
- **Bloqueio externo**: dataset + necessidade de re-treino contínuo.

## Ordem sugerida de retomada

```
#7 (2º push: persistência + MCP)  →  #8 (agente iDMSS)  →  #9 (imagem)  →  #10 (inferência)  →  #11 (treino)
        já tem o domínio                 já tem o ranking        contrato novo      já tem a semente     precisa de dado
```

Racional: fechar o #7 primeiro (persistência/MCP) porque #8, #9 e #10 consultam a
base de causa raiz. Depois a interface (#8), que é o entregável mais visível. Visão
(#9) e inferência (#10) em paralelo. Treino (#11) por último, quando houver dado.

## Rastreio

Backlog e checkboxes por épico nas issues **#6–#11** do GitHub (atualizadas com o que
já foi feito). Este doc é o mapa; as issues são o detalhe acionável.

## Como validar quando retomar (o ambiente que falta)

1. `docker compose up -d` (Kafka, Postgres, Valkey, MinIO) — exercita coleta,
   persistência e o pipeline de eventos.
2. Um **MES real** (ou um mock REST/SQL) para o adapter do #6.
3. **GPU + modelo + dataset** para a visão do #9 e o RF do #8.
4. **Operação/PIM** para elicitar Ishikawa (#7/#10) e rotular dados (#8/#9).

Sem 3 e 4, dá pra validar #6, #7 e a interface do #8 no baseline; o resultado de
mestrado (acurácia dos modelos) depende do dado real.
