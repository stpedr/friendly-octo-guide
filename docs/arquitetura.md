# Arquitetura → código: onde cada bloco vive

Mapa da proposta de arquitetura pro monorepo. Cada bloco tem domínio puro
(testado, sem infra), um host instrumentado (Serilog + OTel via
`Platform.ServiceDefaults`) e pipeline próprio em `.github/workflows/`.

| Bloco da proposta | Pasta | O que já executa (fase 0) |
|---|---|---|
| 1 · Cliente (PWA) | `clients/pwa/` | Login em 2 etapas (senha → TOTP), painel de linha por WSS, RUM via beacon, SW que nunca cacheia `/v1/**` |
| 2 · Gateway/BFF | `src/Gateway/` | YARP, validação de JWT, tabela de rotas negada-por-padrão, RBAC/ABAC na borda, token bucket por usuário |
| 2b · Ingestão de linha (edge) | `src/Edge.ProtocolGateway/` | Tradução Modbus/MQTT → evento canônico, store-and-forward com backpressure explícito, egress Kafka em Avro |
| 3 · Serviços de aplicação | `src/Identity/`, `src/Core.Execution/`, `src/Notifications/` | TOTP RFC 6238 + refresh rotativo; ordem de produção + outbox transacional; escalonamento on-call + push ntfy |
| 3b · Subsistema de IA | `src/Ai/` | Router (tipo → tópico, DLQ com motivo), worker LLM (vLLM OpenAI-compatible, idempotência por job-id) |
| 4 · Dados | `docker-compose.yml`, `schemas/` | Postgres, Valkey, MinIO, VictoriaMetrics; contratos Avro versionados (codec executável em `Platform.Contracts`) |
| Quality gate (2b) | `src/Telemetry.Ingest/` | Faixa física + clock drift + staleness; rejeitada vai pra quarentena com motivo, nunca pro lixo |
| 5 · Preditivo | `src/Predictive/` | EWMA/z-score online com warmup, baseline imune a contaminação, drift predição vs. real; anomalia vira alerta |
| 5/6 · Decision Engine | `src/Decision.Engine/` | Envelope de operação (faixa + degrau máximo), aprovação humana por criticidade, 100% das decisões auditadas com trace-id |
| 5b · Chatbot & Agentes | `src/Chatbot/` | RAG com filtro de visibilidade RBAC, guardrails de ferramenta (`always_ask` pra ação destrutiva), toda conversa vira trace |
| Espinha de observabilidade | `deploy/observability/` | OTel Collector → Loki (logs) / Tempo (traces) / VictoriaMetrics (métricas) → Grafana; log pula pro trace pelo trace-id |
| Fundação de execução | `deploy/` | Namespace por domínio, HPA no gateway, KEDA por lag de fila (scale-to-zero no worker de LLM), Dockerfiles multi-stage |

## Fluxos ponta a ponta que já fecham

**Clique → registro:** PWA → Gateway (JWT + RBAC/ABAC + rate limit) →
Core.Execution → Postgres (agregado + outbox na MESMA transação) → relay →
Kafka `core.eventos.v1`. Cada salto emite log/trace/métrica pro Collector.

**Fábrica → nuvem:** sensor → MQTT (DMZ industrial) → tradução → buffer
store-and-forward → Kafka `linha.telemetria.v1` (Avro) → quality gate →
Postgres (aceita, idempotente) / quarentena (rejeitada, com motivo).

**Telemetria → alerta → celular:** stream → Predictive (z-score) →
`linha.alertas.v1` → Notifications (escada de on-call por severidade) →
push ntfy / e-mail.

**Loop fechado com guardrails:** proposta → Decision.Engine (envelope físico →
criticidade) → aprovado/pendente/rejeitado → `auditoria.decisoes.v1` sempre;
comando aprovado volta pela borda, nunca direto no PLC.

**Job de IA:** serviço publica `ai.jobs.v1` → router (tipo → tópico; DLQ com
motivo) → worker LLM (idempotente por job-id) → `ai.resultados.v1`; falha
reenfileira com tentativa contada até a DLQ.

## Decisões tomadas na implementação

- **Avro sem registry na fase 0.** O codec de `Platform.Contracts` implementa o
  binário do spec (varint zigzag) pro schema fixado em `schemas/`; o Schema
  Registry entra na fase 1 sem mudar um byte do formato. Tópicos de controle
  (jobs, alertas, comandos) trafegam JSON documentado nos `.avsc` até lá.
- **RBAC/ABAC é uma lib compartilhada** (`Platform.AccessControl`): o Identity
  emite as claims (`role`, `attr:*`), o Gateway avalia na borda e o Chatbot
  reavalia nos guardrails — mesma semântica, um só código.
- **Segurança dos tokens:** access token de 5 min só em memória no cliente;
  refresh rotativo de uso único com revogação de família em reuso (detecção de
  roubo); TOTP com janela ±1 e proteção de replay por step.
- **Nada se perde calado:** quarentena no ingest, DLQ na IA, buffer que recusa
  (e grita) em vez de descartar na borda, outbox com backoff — cada caminho de
  falha tem destino e métrica.

## O que fica pra fase 1 (e onde encaixa)

- **Keycloak** assume OIDC; o Identity vira fachada/emissor interno ou é absorvido.
- **OpenBao + External Secrets**: chaves JWT e seeds TOTP saem da config.
- **mTLS/service mesh (Linkerd)**, WAF (Coraza) na frente do Gateway.
- **Valkey** assume rate limit distribuído, idempotência entre réplicas e cache de sessão.
- **pgvector + embeddings workers** substituem a relevância lexical do RAG.
- **Flink/ksqlDB** quando o scoring precisar de janela/join além do por-sensor.
- **MirrorMaker 2 + região standby** (DR), **Velero**, **Harbor/cosign** (o CI já
  tem o degrau de scan Trivy), **ArgoCD** aplicando `deploy/`.
- **Multi-tenant continua em aberto** — como na proposta: decide antes de
  detalhar backup/DR de verdade.
