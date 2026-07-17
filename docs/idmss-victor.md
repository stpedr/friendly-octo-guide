# iDMSS (proposta Victor) — mapa das 4 camadas nos blocos

**Planejamento + progresso — 2026-07-16.** A dissertação do Victor propõe um iDMSS
(intelligent Decision Maker Support System) com um framework de **4 camadas**. Este
doc mostra o mapa 1:1 nos blocos da plataforma e o que já está implementado.

## As 4 camadas → blocos

| Camada (dissertação) | Bloco da plataforma | Estado |
|---|---|---|
| 3.1.1.1 Coleta de dados | `Mes.Connector` (+ edge) | ✅ **implementado** (Épico 1, #6) |
| 3.1.1.2 Processamento (pré-proc + Random Forest) | `Predictive` | 🟡 **ranking pronto; RF externo** |
| 3.1.1.3 Base de conhecimento (Ishikawa) | `Knowledge` | ✅ **classificador + diagrama** (Épico 2, #7) |
| 3.1.1.4 Interface e assistente virtual | `Chatbot` + `Agents` (+ `Decision.Engine`) | ⬜ **wiring pendente** |

## O que já roda (verde no CI)

- **Coleta** — `Mes.Connector` normaliza qualquer MES → `mes.eventos.v1` (defeitos,
  paradas, apontamentos), com quarentena e idempotência.
- **Base de conhecimento** — `IshikawaClassifier` (sintoma → categoria 6M, regras
  interpretáveis/XAI) + `IshikawaDiagram` (espinha de peixe, ranking) + `CausaRaizSeed`
  (semeia hipóteses a partir dos motivos do OEE).
- **Processamento** — `Predictive.Domain.Diagnosis.DiagnosisRanking`: ordena os
  sintomas candidatos por evidência (**ocorrências × peso do modelo**), com
  explicabilidade — cada score carrega a conta que o gerou. O **Random Forest** entra
  trocando o peso; a agregação/ranking é domínio.

## O que falta (e a dependência externa)

- **Camada de interface (agente iDMSS):** costurar `Chatbot` + `Decision.Engine`
  (guardrails, aprovação por criticidade) + `Agents` consumindo `mes.eventos.v1` +
  a base Ishikawa + o ranking. É wiring de IO — o próximo push do Épico 3.
- **O Random Forest treinado:** o `DiagnosisRanking` serve o modelo como **peso**;
  treinar o RF exige **dataset rotulado de falhas do PIM** (Épico 6/7). Até lá, o
  baseline (peso 1.0 = pura frequência) já roda e é honesto.
- **Persistência da base Ishikawa** (pgvector + estruturada) e a **ferramenta MCP**
  pros agentes — 2º push do Épico 2 (#7).

## Por que isto já é valor

O iDMSS do Victor não precisa de plataforma nova: **coleta, base de conhecimento e
processamento estão no lugar**, testados e explicáveis. O que resta é a costura
conversacional (agente) e o dado real pra treinar o modelo — trabalho de integração e
de campo, não de arquitetura.
