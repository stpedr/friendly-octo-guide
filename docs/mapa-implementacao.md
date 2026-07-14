---
id: mapa-implementacao
title: Mapa completo da implementação
sidebar_position: 2
hide_title: true
hide_table_of_contents: true
wrapperClassName: architecture-map-page
description: Mapa técnico completo dos clientes, serviços, tópicos, dados, observabilidade e deploy da plataforma de linha.
---

<header className="map-page-header">
  <div className="architecture-eyebrow">Visão técnica expandida · validada contra o código em 14/07/2026</div>
  <h1>Mapa completo da implementação</h1>
  <div className="architecture-lede">
    Clientes, acesso, APIs, edge industrial, tópicos Kafka, consumers, IA, stores, observabilidade e entrega
    GitOps em uma única visão. Linhas contínuas representam fluxos implementados; linhas tracejadas representam
    configuração pronta, dependência externa ou integração ainda parcial.
  </div>
  <a className="map-back-link" href="./arquitetura">← Voltar para a arquitetura detalhada</a>
</header>

<div className="architecture-legend map-legend">
  <span><i className="legend-dot client-dot"></i>Cliente</span>
  <span><i className="legend-dot access-dot"></i>Acesso / Gateway</span>
  <span><i className="legend-dot edge-dot"></i>Borda industrial</span>
  <span><i className="legend-dot service-dot"></i>Serviços .NET</span>
  <span><i className="legend-dot ai-dot"></i>IA assíncrona</span>
  <span><i className="legend-dot data-dot"></i>Dados</span>
  <span><i className="legend-dot obs-dot"></i>Observabilidade</span>
</div>

<nav className="map-section-nav" aria-label="Níveis de leitura do mapa">
  <a href="#visao-macro">Visão macro</a>
  <a href="#paineis-tecnicos">Painéis técnicos</a>
  <a href="#mapa-completo">Mapa completo</a>
</nav>

<section className="diagram-panel diagram-panel--macro" id="visao-macro">
  <div className="diagram-panel-heading">
    <div className="diagram-panel-title">
      <span className="diagram-panel-index">MACRO</span>
      <div>
        <h2>Visão macro da plataforma</h2>
        <p>O caminho principal em uma leitura: quem entra, onde a regra roda, como os eventos circulam, onde os dados ficam e como tudo é operado.</p>
      </div>
    </div>
    <DiagramDownload filename="plataforma-visao-macro" />
  </div>

```mermaid
flowchart TB
  subgraph USERFLOW["Fluxo do usuário"]
    direction LR
    USERS["Operador<br/>PWA · Tauri"] --> ACCESS["Acesso seguro<br/>WAF · Gateway · Identity"] --> SERVICES["Domínio .NET<br/>Core · Chat · Knowledge · Agents"]
  end

  subgraph EVENTFLOW["Fluxo industrial e assíncrono"]
    direction LR
    EDGE["Fábrica<br/>PLC · MQTT · Edge"] --> KAFKA[("Kafka<br/>eventos e filas")] --> WORKERS["Processamento<br/>Ingest · Predictive · Decision · IA"]
  end

  DATA["Dados e modelos<br/>PostgreSQL · MinIO · MLflow"]
  OBS["Observabilidade<br/>OTel · Grafana · Loki · Tempo · VM"]
  DELIVERY["Entrega<br/>CI · ArgoCD · Helm · KEDA"]
  SERVICES --> KAFKA
  SERVICES --> DATA
  WORKERS --> DATA
  OBS -. mede tudo .-> USERFLOW
  OBS -. mede tudo .-> EVENTFLOW
  DELIVERY -. implanta e escala .-> USERFLOW
  DELIVERY -. implanta e escala .-> EVENTFLOW

  classDef client fill:#e8f2ff,stroke:#2563eb,color:#10233f;
  classDef access fill:#fff5df,stroke:#d97706,color:#3b2a0b;
  classDef edge fill:#e5faf6,stroke:#0f766e,color:#123b38;
  classDef service fill:#f1ebff,stroke:#7c3aed,color:#2e1a54;
  classDef data fill:#e9f8ef,stroke:#15803d,color:#173d24;
  classDef obs fill:#fff8df,stroke:#ca8a04,color:#3c300a;
  class USERS client;
  class ACCESS access;
  class EDGE edge;
  class SERVICES,KAFKA,WORKERS service;
  class DATA data;
  class OBS,DELIVERY obs;
```

  <div className="diagram-reading-path">
    <strong>Fluxo principal</strong>
    <span>Cliente → acesso → serviços → Kafka → processamento → dados</span>
    <span>Observabilidade e GitOps atravessam todas as etapas.</span>
  </div>
</section>

<div className="technical-panels-intro" id="paineis-tecnicos">
  <span className="architecture-eyebrow">DETALHE PROGRESSIVO</span>
  <h2>Painéis técnicos por domínio</h2>
  <p>Cada painel isola um contexto operacional. As conexões externas aparecem apenas como entrada ou saída, para manter portas, contratos e responsabilidades legíveis.</p>
</div>

<section className="diagram-panel" id="clientes-acesso">
  <div className="diagram-panel-heading">
    <div className="diagram-panel-title">
      <span className="diagram-panel-index">01</span>
      <div>
        <h2>Clientes, identidade e acesso HTTP</h2>
        <p>Login, TOTP, autorização, proteção de borda, limitação distribuída e roteamento público.</p>
      </div>
    </div>
    <DiagramDownload filename="01-clientes-identidade-acesso" />
  </div>

```mermaid
flowchart LR
  ADMIN["Admin / operador"] --> PWA["PWA :8081<br/>senha → TOTP · JWT · RUM"]
  PWA -->|HTTPS /v1| WAF["NGINX + ModSecurity<br/>:8443"]
  WAF --> GW["Gateway .NET :5180<br/>YARP · RBAC/ABAC · 20 req/10s"]
  GW --> ID["Identity API :5101<br/>login · TOTP · refresh rotativo"]
  GW --> APIS["APIs .NET<br/>Core · Chat · Knowledge · Agents"]
  GW --> VALKEY[("Valkey :6379<br/>token bucket Lua")]
  ID -. OIDC .-> KEYCLOAK["Keycloak<br/>usuários no cluster"]
  OPENBAO["OpenBao + External Secrets"] -. segredos .-> ID
  OPENBAO -. JWT .-> GW
  PWA -->|sendBeacon /rum| GW

  classDef client fill:#e8f2ff,stroke:#2563eb,color:#10233f;
  classDef access fill:#fff5df,stroke:#d97706,color:#3b2a0b;
  classDef service fill:#f1ebff,stroke:#7c3aed,color:#2e1a54;
  class ADMIN,PWA client;
  class WAF,GW,ID,VALKEY,KEYCLOAK,OPENBAO access;
  class APIS service;
```
</section>

<section className="diagram-panel" id="servicos-dotnet">
  <div className="diagram-panel-heading">
    <div className="diagram-panel-title">
      <span className="diagram-panel-index">02</span>
      <div>
        <h2>Serviços de aplicação .NET</h2>
        <p>Responsabilidades síncronas, chamadas autorizadas e a fronteira transacional entre banco e eventos.</p>
      </div>
    </div>
    <DiagramDownload filename="02-servicos-dotnet" />
  </div>

```mermaid
flowchart LR
  GW["Gateway :5180"] --> CORE["Core.Execution :5102<br/>ordens · estados · outbox"]
  GW --> CHAT["Chatbot :5103<br/>RAG · guardrails · MCP"]
  GW --> KNOW["Knowledge :5104<br/>GraphQL · JSONB · pgvector"]
  GW --> AGENTS["Agents :5105<br/>diagnóstico · relatório · proposta"]
  CHAT -->|contexto RBAC| KNOW
  CHAT -->|always_ask| CORE
  CORE -->|mesma transação| PG[("PostgreSQL<br/>work_orders + outbox")]
  CORE -->|OutboxRelay| EVENTS[("core.eventos.v1")]
  AGENTS --> REPORTS[("relatorios.diarios.v1")]

  classDef access fill:#fff5df,stroke:#d97706,color:#3b2a0b;
  classDef service fill:#f1ebff,stroke:#7c3aed,color:#2e1a54;
  classDef data fill:#e9f8ef,stroke:#15803d,color:#173d24;
  class GW access;
  class CORE,CHAT,KNOW,AGENTS service;
  class PG,EVENTS,REPORTS data;
```
</section>

<section className="diagram-panel" id="edge-eventos">
  <div className="diagram-panel-heading">
    <div className="diagram-panel-title">
      <span className="diagram-panel-index">03</span>
      <div>
        <h2>Edge industrial e ingestão de telemetria</h2>
        <p>Do protocolo de fábrica ao evento canônico, com buffer, validação, persistência e detecção preditiva.</p>
      </div>
    </div>
    <DiagramDownload filename="03-edge-ingestao-telemetria" />
  </div>

```mermaid
flowchart LR
  SENSOR["PLC / sensor"] -->|MQTT · Modbus| MQTT["Mosquitto :1883<br/>linha/+/sensor/+"]
  MQTT --> EDGE["Edge.ProtocolGateway<br/>evento canônico · device identity"]
  EDGE --> BUFFER["Store-and-forward<br/>backpressure explícito"]
  BUFFER --> TELEMETRY[("linha.telemetria.v1<br/>Avro SensorReading")]
  TELEMETRY --> INGEST["Telemetry.Ingest<br/>range · clock drift · staleness"]
  TELEMETRY --> PRED["Predictive<br/>EWMA · z-score · drift"]
  TELEMETRY --> ARCHIVER["Data.Archiver<br/>JSONL.gz · replay idempotente"]
  INGEST --> PG[("PostgreSQL<br/>ON CONFLICT")]
  INGEST --> QUARANTINE[("telemetria.quarentena.v1")]
  PRED --> ALERTS[("linha.alertas.v1")]
  ARCHIVER --> MINIO[("MinIO<br/>linha-lake")]

  classDef edge fill:#e5faf6,stroke:#0f766e,color:#123b38;
  classDef service fill:#f1ebff,stroke:#7c3aed,color:#2e1a54;
  classDef data fill:#e9f8ef,stroke:#15803d,color:#173d24;
  class SENSOR,MQTT,EDGE,BUFFER edge;
  class INGEST,PRED,ARCHIVER service;
  class TELEMETRY,PG,QUARANTINE,ALERTS,MINIO data;
```
</section>

<section className="diagram-panel" id="decisoes-streaming">
  <div className="diagram-panel-heading">
    <div className="diagram-panel-title">
      <span className="diagram-panel-index">04</span>
      <div>
        <h2>Alertas, decisões e streaming contínuo</h2>
        <p>Escalonamento operacional, aprovação humana, auditoria e agregações contínuas no Kafka.</p>
      </div>
    </div>
    <DiagramDownload filename="04-alertas-decisoes-streaming" />
  </div>

```mermaid
flowchart LR
  ALERTS[("linha.alertas.v1")] --> NOTIFY["Notifications<br/>on-call · ntfy · e-mail"]
  ALERTS --> AGENTS["Agents<br/>correlação e diagnóstico"]
  ALERTS --> KSQL["ksqlDB :8088<br/>janelas versionadas"]
  CORE[("core.eventos.v1")] --> KSQL
  KSQL --> STORM[("alertas.tempestade.v1")]
  KSQL --> THROUGHPUT[("linha.throughput.v1")]
  COMMANDS[("comandos.propostos.v1")] --> DECISION["Decision.Engine<br/>envelope · degrau · regra"]
  DECISION --> APPROVED[("comandos.aprovados.v1")]
  DECISION --> PENDING[("comandos.pendentes.v1")]
  DECISION --> AUDIT[("auditoria.decisoes.v1<br/>desfecho + trace-id")]

  classDef service fill:#f1ebff,stroke:#7c3aed,color:#2e1a54;
  classDef data fill:#e9f8ef,stroke:#15803d,color:#173d24;
  class NOTIFY,AGENTS,KSQL,DECISION service;
  class ALERTS,CORE,STORM,THROUGHPUT,COMMANDS,APPROVED,PENDING,AUDIT data;
```
</section>

<section className="diagram-panel" id="ia-assincrona">
  <div className="diagram-panel-heading">
    <div className="diagram-panel-title">
      <span className="diagram-panel-index">05</span>
      <div>
        <h2>Subsistema assíncrono de IA</h2>
        <p>Roteamento por modalidade, workers idempotentes, serving em GPU, retry controlado e DLQ.</p>
      </div>
    </div>
    <DiagramDownload filename="05-subsistema-ia-assincrona" />
  </div>

```mermaid
flowchart LR
  CHAT["Chatbot / serviço solicitante"] --> JOBS[("ai.jobs.v1")]
  JOBS --> ROUTER["Ai.Router<br/>tipo → tópico · attempts"]
  ROUTER --> LLMQ[("ai.jobs.llm.v1")]
  ROUTER --> VISIONQ[("ai.jobs.vision.v1")]
  ROUTER --> EMBEDQ[("ai.jobs.embedding.v1")]
  ROUTER --> DLQ[("ai.jobs.dlq.v1")]
  LLMQ --> LLM["Worker LLM"] --> VLLM["llama.cpp / vLLM :8000"]
  VISIONQ --> VISION["Worker Vision"] --> VSERVE["Vision serving :8001"]
  EMBEDQ --> EMBED["Worker Embeddings"] --> ESERVE["Embeddings :8002"]
  VLLM --> RESULT[("ai.resultados.v1")]
  VSERVE --> RESULT
  ESERVE --> RESULT
  LLM -. falha + 1 attempt .-> JOBS
  VISION -. falha + 1 attempt .-> JOBS
  EMBED -. falha + 1 attempt .-> JOBS

  classDef service fill:#f1ebff,stroke:#7c3aed,color:#2e1a54;
  classDef ai fill:#fff0f2,stroke:#e11d48,color:#4a1723;
  classDef wait fill:#f4f4f5,stroke:#71717a,color:#27272a,stroke-dasharray:5 5;
  class CHAT service;
  class JOBS,ROUTER,LLMQ,VISIONQ,EMBEDQ,DLQ,LLM,VISION,EMBED,RESULT ai;
  class VLLM,VSERVE,ESERVE wait;
```
</section>

<section className="diagram-panel" id="dados-linhagem">
  <div className="diagram-panel-heading">
    <div className="diagram-panel-title">
      <span className="diagram-panel-index">06</span>
      <div>
        <h2>Persistência, lake, modelos e linhagem</h2>
        <p>Dados quentes, recuperação, objetos frios, schemas, experimentos e o ponto ainda pendente da linhagem.</p>
      </div>
    </div>
    <DiagramDownload filename="06-dados-lake-modelos-linhagem" />
  </div>

```mermaid
flowchart LR
  APIS["Core · Ingest · Knowledge"] --> PG[("PostgreSQL 17 + pgvector<br/>:5432")]
  PG --> REPLICA[("Réplica streaming<br/>:5433")]
  PG --> BACKUP["pg_dump diário<br/>+ WAL archive"]
  ARCHIVER["Data.Archiver"] --> MINIO[("MinIO :9000/:9001<br/>linha-lake")]
  PRED["Predictive"] --> MLFLOW["MLflow :5500<br/>modelo ativo"]
  SCHEMAS["schemas/*.avsc"] --> APICURIO["Apicurio :8085"]
  APICURIO --> KAFKA[("Kafka contracts")]
  ARCHIVER --> LINEAGE[("linhagem.openlineage.v1")]
  LINEAGE -. falta bridge HTTP .-> MARQUEZ["Marquez :5001 / :3012"]

  classDef service fill:#f1ebff,stroke:#7c3aed,color:#2e1a54;
  classDef data fill:#e9f8ef,stroke:#15803d,color:#173d24;
  classDef gap fill:#fff0f3,stroke:#b4233f,color:#4a1723,stroke-dasharray:5 5;
  class APIS,ARCHIVER,PRED service;
  class PG,REPLICA,BACKUP,MINIO,MLFLOW,SCHEMAS,APICURIO,KAFKA,LINEAGE data;
  class MARQUEZ gap;
```
</section>

<section className="diagram-panel" id="observabilidade">
  <div className="diagram-panel-heading">
    <div className="diagram-panel-title">
      <span className="diagram-panel-index">07</span>
      <div>
        <h2>Espinha de observabilidade</h2>
        <p>Uma emissão OTLP comum para logs, traces e métricas, com correlação, SLO, custo e checks sintéticos.</p>
      </div>
    </div>
    <DiagramDownload filename="07-espinha-observabilidade" />
  </div>

```mermaid
flowchart LR
  SOURCES["Gateway · APIs · Edge<br/>consumers · workers IA"] --> DEFAULTS["Platform.ServiceDefaults<br/>Serilog · Activity · Meter · PII masking"]
  DEFAULTS -->|OTLP| OTEL["OTel Collector<br/>:4317 / :4318"]
  OTEL -->|logs| LOKI["Loki :3100"]
  OTEL -->|traces| TEMPO["Tempo :3200"]
  OTEL -->|remote_write| VM["VictoriaMetrics :8428"]
  LOKI --> GRAFANA["Grafana OSS :3000<br/>logs ↔ traces"]
  TEMPO --> GRAFANA
  VM --> GRAFANA
  PYRRA["Pyrra :9099<br/>SLO · error budget"] --> VM
  OPENCOST["OpenCost :9003<br/>FinOps"] <--> VM
  KUMA["Uptime Kuma :3001<br/>32 checks"] -. health/UI .-> SOURCES

  classDef service fill:#f1ebff,stroke:#7c3aed,color:#2e1a54;
  classDef obs fill:#fff8df,stroke:#ca8a04,color:#3c300a;
  class SOURCES service;
  class DEFAULTS,OTEL,LOKI,TEMPO,VM,GRAFANA,PYRRA,OPENCOST,KUMA obs;
```
</section>

<section className="diagram-panel" id="plataforma-entrega">
  <div className="diagram-panel-heading">
    <div className="diagram-panel-title">
      <span className="diagram-panel-index">08</span>
      <div>
        <h2>Plataforma, segurança e entrega</h2>
        <p>Do commit assinado ao workload escalável, com GitOps, mTLS, segredos e recuperação de desastre.</p>
      </div>
    </div>
    <DiagramDownload filename="08-plataforma-seguranca-entrega" />
  </div>

```mermaid
flowchart LR
  GIT["Commit em main"] --> CI["GitHub Actions<br/>TDD · cobertura · Trivy · cosign"]
  CI --> IMAGE["Imagem por commit"] --> ARGO["ArgoCD<br/>sync · prune · selfHeal"]
  ARGO --> HELM["Helm<br/>16 workloads · 4 namespaces"]
  HELM --> WORKLOADS["APIs · consumers · workers"]
  SCALE["HPA + KEDA<br/>CPU · lag · GPU scale-to-zero"] -. escala .-> WORKLOADS
  MESH["Linkerd mTLS"] -. protege .-> WORKLOADS
  OPENBAO["OpenBao + External Secrets"] -. injeta segredos .-> WORKLOADS
  DR["Velero · MinIO · MirrorMaker 2<br/>warm standby"] -. restaura e replica .-> WORKLOADS
  COMPOSE["Docker Compose local"] --> TOOLS["Flipt :4242 · Docusaurus :3003<br/>stack local"]

  classDef access fill:#fff5df,stroke:#d97706,color:#3b2a0b;
  classDef service fill:#f1ebff,stroke:#7c3aed,color:#2e1a54;
  classDef obs fill:#fff8df,stroke:#ca8a04,color:#3c300a;
  class GIT,CI,IMAGE,ARGO,HELM access;
  class WORKLOADS service;
  class SCALE,MESH,OPENBAO,DR,COMPOSE,TOOLS obs;
```
</section>

<section className="diagram-panel diagram-panel--complete" id="mapa-completo">
  <div className="diagram-panel-heading">
    <div className="diagram-panel-title">
      <span className="diagram-panel-index">FULL</span>
      <div>
        <h2>Mapa integral de implementação</h2>
        <p>Referência de engenharia com todos os serviços, portas, tópicos, stores, estados e integrações em uma única prancha.</p>
      </div>
    </div>
    <DiagramDownload filename="plataforma-mapa-completo" />
  </div>

```mermaid
flowchart TB
  subgraph USERS["Pessoas e clientes"]
    ADMIN["Admin / operador"]
    PWA["OK LOCAL · PWA :8081<br/>clients/pwa<br/>senha → TOTP · SW · RUM"]
    DESKTOP["OK CÓDIGO · Tauri Desktop<br/>clients/desktop"]
    MOBILE["AGUARDA SDK · Tauri Mobile<br/>clients/mobile"]
    OBSCLIENT["OK CÓDIGO · Cliente de observabilidade<br/>Web/PWA + Tauri · TOTP local"]
    ADMIN --> PWA
    ADMIN -. empacotamento .-> DESKTOP
    ADMIN -. empacotamento .-> MOBILE
    ADMIN --> OBSCLIENT
  end

  subgraph ACCESS["Acesso, identidade e borda HTTP"]
    WAF["OK LOCAL · NGINX + ModSecurity CRS :8443<br/>perfil waf"]
    GW["OK LOCAL · Gateway .NET :5180<br/>YARP · JWT · RBAC/ABAC · rate limit 20/10s<br/>deny-by-default · traceparent · /rum"]
    ID["OK LOCAL · Identity API :5101<br/>/login · /totp · /refresh<br/>TOTP RFC 6238 · refresh rotativo"]
    KEYCLOAK["OK CONFIG · Keycloak<br/>fonte de usuários/OIDC no deploy Pi"]
    OPENBAO["OK CONFIG · OpenBao + External Secrets<br/>JWT, TOTP seed e credenciais"]
    VALKEY["OK LOCAL · Valkey :6379<br/>token bucket Lua distribuído"]
    PWA -->|"HTTPS /v1/* · JWT"| WAF
    WAF --> GW
    GW -->|"proxy público /v1/auth/*"| ID
    GW -->|"rate limit distribuído"| VALKEY
    ID -. "OIDC/password grant quando configurado" .-> KEYCLOAK
    OPENBAO -. "PlatformSecrets / ExternalSecret" .-> ID
    OPENBAO -. "chave JWT" .-> GW
    PWA -->|"sendBeacon /rum"| GW
  end

  subgraph APIS["APIs síncronas .NET"]
    CORE["OK LOCAL · Core.Execution :5102<br/>ordens + máquina de estados<br/>agregado e outbox na mesma transação"]
    CHAT["OK LOCAL · Chatbot :5103<br/>RAG · guardrails · REST + MCP"]
    KNOW["OK LOCAL · Knowledge :5104<br/>GraphQL autenticado · JSONB + pgvector<br/>filtro RBAC na consulta"]
    AGENTS["OK LOCAL · Agents :5105<br/>correlação · diagnóstico · relatório diário<br/>proposta de ação protegida"]
    GW -->|"/v1/core/* operador/admin"| CORE
    GW -->|"/v1/chat/* operador/admin"| CHAT
    GW -->|"/v1/knowledge/* operador/admin"| KNOW
    GW -->|"/v1/agents/* operador/admin"| AGENTS
    CHAT -->|"contexto autorizado"| KNOW
    CHAT -->|"ferramenta com always_ask"| CORE
  end

  subgraph EDGE["Linha industrial / edge"]
    SENSOR["PLC / sensor / dispositivo"]
    MQTT["OK LOCAL · Mosquitto :1883<br/>linha/+/sensor/+"]
    EDGEGW["OK LOCAL · Edge.ProtocolGateway<br/>Modbus/MQTT → evento canônico<br/>identidade do device + OTA policy"]
    BUFFER["OK CÓDIGO · store-and-forward<br/>capacidade explícita · backpressure"]
    SENSOR -->|"MQTT / Modbus"| MQTT
    MQTT --> EDGEGW
    EDGEGW --> BUFFER
  end

  subgraph BUS["Kafka KRaft :9092 · contratos Avro/JSON versionados"]
    T_TELEMETRY[("linha.telemetria.v1<br/>Avro SensorReading")]
    T_QUARANTINE[("linha.telemetria.quarentena.v1")]
    T_CORE[("core.eventos.v1")]
    T_ALERTS[("linha.alertas.v1")]
    T_STORM[("linha.alertas.tempestade.v1")]
    T_THROUGHPUT[("linha.throughput.v1")]
    T_COMMANDS[("linha.comandos.propostos.v1")]
    T_APPROVED[("linha.comandos.aprovados.v1")]
    T_PENDING[("linha.comandos.pendentes.v1")]
    T_AUDIT[("auditoria.decisoes.v1")]
    T_AI[("ai.jobs.v1")]
    T_LLM[("ai.jobs.llm.v1")]
    T_VISION[("ai.jobs.vision.v1")]
    T_EMBED[("ai.jobs.embedding.v1")]
    T_AI_RESULT[("ai.resultados.v1")]
    T_AI_DLQ[("ai.jobs.dlq.v1")]
    T_LINEAGE[("linhagem.openlineage.v1")]
    T_REPORT[("relatorios.diarios.v1")]
    BUFFER -->|"producer idempotente · bytes Avro"| T_TELEMETRY
    CORE -->|"OutboxRelay"| T_CORE
    AGENTS -->|"DailyReportService"| T_REPORT
  end

  subgraph STREAM["Consumidores e decisões assíncronas"]
    INGEST["OK LOCAL · Telemetry.Ingest<br/>range · clock drift · staleness<br/>commit manual depois do sink"]
    PRED["OK LOCAL · Predictive<br/>EWMA / z-score · warmup<br/>baseline sem contaminação · drift"]
    NOTIFY["OK LOCAL · Notifications<br/>escada on-call · ntfy<br/>e-mail estruturado em log na fase atual"]
    DECISION["OK LOCAL · Decision.Engine<br/>envelope físico · degrau máximo<br/>autoaprova / pede humano / rejeita"]
    ARCHIVER["OK LOCAL · Data.Archiver<br/>batch por partição · JSONL.gz<br/>partição Hive · replay idempotente"]
    KSQL["OK LOCAL · ksqlDB :8088<br/>janelas contínuas versionadas"]
    T_TELEMETRY --> INGEST
    INGEST -->|"inválida + motivo"| T_QUARANTINE
    T_TELEMETRY --> PRED
    PRED -->|"anomalia"| T_ALERTS
    T_ALERTS --> NOTIFY
    T_ALERTS --> AGENTS
    T_COMMANDS --> DECISION
    DECISION --> T_APPROVED
    DECISION --> T_PENDING
    DECISION -->|"todo desfecho + trace-id"| T_AUDIT
    T_TELEMETRY --> ARCHIVER
    ARCHIVER -->|"OpenLineage RunEvent"| T_LINEAGE
    T_ALERTS --> KSQL
    T_CORE --> KSQL
    KSQL --> T_STORM
    KSQL --> T_THROUGHPUT
  end

  subgraph AI["Subsistema assíncrono de IA"]
    ROUTER["OK LOCAL · Ai.Router<br/>tipo → tópico · attempts → DLQ<br/>produce antes do commit"]
    LLMWORKER["OK PROCESSO · Worker LLM<br/>idempotência por job-id · retry"]
    VISIONWORKER["OK PROCESSO · Worker Vision<br/>OCR / caption / classify"]
    EMBEDWORKER["OK PROCESSO · Worker Embeddings<br/>OpenAI-compatible /v1/embeddings"]
    VLLM["AGUARDA GPU · llama.cpp/vLLM na Jetson :8000"]
    VISIONSERVE["AGUARDA GPU · vision serving :8001"]
    EMBEDSERVE["AGUARDA GPU · embeddings serving :8002"]
    CHAT -->|"job assíncrono"| T_AI
    T_AI --> ROUTER
    ROUTER --> T_LLM
    ROUTER --> T_VISION
    ROUTER --> T_EMBED
    ROUTER --> T_AI_DLQ
    T_LLM --> LLMWORKER
    T_VISION --> VISIONWORKER
    T_EMBED --> EMBEDWORKER
    LLMWORKER -. "OpenAI /v1/chat/completions" .-> VLLM
    VISIONWORKER -. "/v1/vision" .-> VISIONSERVE
    EMBEDWORKER -. "/v1/embeddings" .-> EMBEDSERVE
    LLMWORKER --> T_AI_RESULT
    VISIONWORKER --> T_AI_RESULT
    EMBEDWORKER --> T_AI_RESULT
    LLMWORKER -->|"falha: attempts + 1"| T_AI
    VISIONWORKER -->|"falha: attempts + 1"| T_AI
    EMBEDWORKER -->|"falha: attempts + 1"| T_AI
  end

  subgraph DATA["Persistência, lake, contratos e linhagem"]
    PG["OK LOCAL · PostgreSQL 17 + pgvector :5432<br/>linha · knowledge · marquez<br/>WAL archive habilitado"]
    PGREPLICA["OK LOCAL · réplica streaming :5433"]
    PGBACKUP["OK LOCAL · pg_dump diário + retenção"]
    MINIO["OK LOCAL · MinIO :9000/:9001<br/>bucket linha-lake"]
    APICURIO["OK LOCAL · Apicurio :8085<br/>schemas/*.avsc · registro idempotente"]
    MLFLOW["OK LOCAL · MLflow :5500<br/>experimentos + modelo ativo"]
    MARQUEZ["OK LOCAL · Marquez API :5001 / Web :3012<br/>UI e banco ativos"]
    CORE -->|"work_orders + outbox · transação única"| PG
    INGEST -->|"telemetria · ON CONFLICT idempotente"| PG
    KNOW -->|"documentos JSONB + vector HNSW"| PG
    PG -->|"WAL streaming"| PGREPLICA
    PG --> PGBACKUP
    ARCHIVER -->|"S3 PutObject"| MINIO
    PRED -->|"alias/modelo ativo"| MLFLOW
    APICURIO -. "publica schemas do repositório" .-> BUS
    T_LINEAGE -. "LACUNA: falta bridge Kafka → API OpenLineage" .-> MARQUEZ
  end

  subgraph OBS["Espinha única de observabilidade"]
    DEFAULTS["OK CÓDIGO · Platform.ServiceDefaults<br/>Serilog + ActivitySource + Meter<br/>resource service.name · PII masking"]
    OTEL["OK LOCAL · OTel Collector :4317/:4318<br/>resource + batch"]
    LOKI["OK LOCAL · Loki :3100<br/>logs estruturados"]
    TEMPO["OK LOCAL · Tempo :3200<br/>traces W3C"]
    VM["OK LOCAL · VictoriaMetrics :8428<br/>métricas + TSDB"]
    GRAFANA["OK LOCAL · Grafana OSS :3000<br/>logs ↔ traces por trace_id"]
    PYRRA["OK LOCAL · Pyrra :9099<br/>SLO + error budget"]
    OPENCOST["OK LOCAL · OpenCost :9003<br/>FinOps sobre métricas"]
    KUMA["OK LOCAL · Uptime Kuma :3001<br/>32 checks sintéticos"]
    DEFAULTS -->|"OTLP"| OTEL
    GW -. "OTLP" .-> OTEL
    ID -. "OTLP" .-> OTEL
    CORE -. "OTLP" .-> OTEL
    CHAT -. "OTLP" .-> OTEL
    KNOW -. "OTLP" .-> OTEL
    AGENTS -. "OTLP" .-> OTEL
    EDGEGW -. "OTLP" .-> OTEL
    INGEST -. "OTLP" .-> OTEL
    PRED -. "OTLP" .-> OTEL
    NOTIFY -. "OTLP" .-> OTEL
    DECISION -. "OTLP" .-> OTEL
    ARCHIVER -. "OTLP" .-> OTEL
    ROUTER -. "OTLP" .-> OTEL
    LLMWORKER -. "OTLP" .-> OTEL
    OTEL -->|"logs OTLP/HTTP"| LOKI
    OTEL -->|"traces OTLP/gRPC"| TEMPO
    OTEL -->|"Prometheus remote_write"| VM
    LOKI --> GRAFANA
    TEMPO --> GRAFANA
    VM --> GRAFANA
    PYRRA -->|"recording rules"| VM
    OPENCOST <-->|"Prometheus API"| VM
    KUMA -. "HTTP health/UI" .-> PWA
    KUMA -. "HTTP health/UI" .-> GW
    KUMA -. "HTTP health/UI" .-> GRAFANA
  end

  subgraph PLATFORM["Plataforma de engenharia e entrega"]
    FLIPT["OK LOCAL · Flipt :4242<br/>feature flags self-hosted"]
    DOCS["OK LOCAL · Docusaurus :3003<br/>esta página versionada"]
    COMPOSE["OK LOCAL · docker-compose.yml<br/>base + ha + waf + stream + plataforma-eng"]
    HELM["OK CONFIG · Helm<br/>16 workloads · 4 namespaces"]
    ARGO["OK CONFIG · ArgoCD<br/>sync · prune · selfHeal"]
    SCALE["OK CONFIG · HPA + KEDA<br/>HTTP por CPU · workers por lag<br/>GPU scale-to-zero"]
    MESH["OK CONFIG · Linkerd mTLS"]
    DR["OK CONFIG · Velero + MinIO<br/>MirrorMaker 2 · warm standby"]
    CI["OK CÓDIGO · GitHub Actions<br/>TDD gate · cobertura · Trivy · cosign"]
    COMPOSE --> FLIPT
    COMPOSE --> DOCS
    HELM --> SCALE
    ARGO --> HELM
    MESH -. "sidecar injection" .-> HELM
    DR -. "restore/replicação" .-> HELM
    CI -->|"imagem por commit"| ARGO
  end

  classDef client fill:#e8f2ff,stroke:#2563eb,color:#10233f;
  classDef access fill:#fff5df,stroke:#d97706,color:#3b2a0b;
  classDef edge fill:#e5faf6,stroke:#0f766e,color:#123b38;
  classDef service fill:#f1ebff,stroke:#7c3aed,color:#2e1a54;
  classDef ai fill:#fff0f2,stroke:#e11d48,color:#4a1723;
  classDef data fill:#e9f8ef,stroke:#15803d,color:#173d24;
  classDef obs fill:#fff8df,stroke:#ca8a04,color:#3c300a;
  classDef wait fill:#f4f4f5,stroke:#71717a,color:#27272a,stroke-dasharray: 5 5;
  class PWA,DESKTOP,OBSCLIENT client;
  class WAF,GW,ID,KEYCLOAK,OPENBAO,VALKEY access;
  class MQTT,EDGEGW,BUFFER edge;
  class CORE,CHAT,KNOW,AGENTS,INGEST,PRED,NOTIFY,DECISION,ARCHIVER,KSQL service;
  class ROUTER,LLMWORKER,VISIONWORKER,EMBEDWORKER ai;
  class PG,PGREPLICA,PGBACKUP,MINIO,APICURIO,MLFLOW,MARQUEZ data;
  class DEFAULTS,OTEL,LOKI,TEMPO,VM,GRAFANA,PYRRA,OPENCOST,KUMA obs;
  class MOBILE,VLLM,VISIONSERVE,EMBEDSERVE wait;
```

</section>

## Leitura rápida

- **Do usuário ao banco:** PWA → WAF → Gateway → Core.Execution → PostgreSQL + outbox → Kafka.
- **Da fábrica ao alerta:** sensor → MQTT → edge → Kafka → quality gate/preditivo → notifications → ntfy.
- **Da fila à IA:** `ai.jobs.v1` → router → worker especializado → serving GPU → `ai.resultados.v1` ou DLQ.
- **De qualquer serviço à operação:** OTLP → Collector → Loki/Tempo/VictoriaMetrics → Grafana.
- **Do Git ao cluster:** CI → imagem assinada → ArgoCD → Helm → HPA/KEDA/Linkerd/External Secrets.
