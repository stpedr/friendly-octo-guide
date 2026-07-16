# Carga operacional: faseamento vs. time de 4

**Status: registrado em 2026-07 — a arquitetura-alvo é grande demais pra ser o
"dia 1". Este doc separa o que roda agora do que é estado final, e ancora a IA
no hardware real.**

## O problema honesto

O README assume **time de 4 pessoas**. A arquitetura-alvo pede operar, ao mesmo
tempo: ~16 serviços .NET, Kafka, Kubernetes multi-região, service mesh (mTLS),
serving de GPU, stack Grafana completo, Keycloak, Harbor+Trivy+cosign,
Feast/MLflow, OpenBao, Velero, Pyrra. Isso é excelente engenharia e um **risco
de sustentação**: com 4 pessoas, operar tudo isso vira o trabalho — e o produto
para. O diagrama descreve o **estado final**, não o ponto de partida.

Regra deste doc: **cada peça de plataforma só entra quando a dor que ela resolve
já existe.** Complexidade sem dor correspondente é custo puro pra um time pequeno.

## O hardware real (âncora de tudo)

| Nó | Papel | Limite que importa |
|---|---|---|
| Raspberry Pi 4/5 (8 GB, ARM64) | edge + core (`docker-compose.pi.yml`) | perfil padrão ≈ 3,5 GB RAM; com 4 GB só o núcleo edge |
| Servidor GPU — **1× RTX A2000 Ada** | serving de IA (vLLM) | **16 GB VRAM**, 32 GB RAM, 1 TB disco |

Não é "GPU pool". É **uma** GPU de 16 GB. Isso muda a história de IA da
arquitetura — ver a seção seguinte, que é a correção mais importante deste doc.

## A realidade da IA em 16 GB (correção de premissa)

A arquitetura diz "LLM auto-hospedado no GPU pool que já existe (zero custo por
token)" e "um Deployment K8s por modelo" (LLM + visão + embeddings, cada um seu
pod). **Em 16 GB de VRAM isso não roda tudo ao mesmo tempo.** Ordem de grandeza:

- um LLM open-weight **7–8B quantizado** (Llama/Qwen/Mistral, AWQ/GPTQ 4-bit)
  ocupa ~5–9 GB só de pesos, mais o KV-cache do contexto → **cabe um, com folga
  modesta**;
- 13–14B quantizado cabe apertado, sem espaço pra rodar visão/embeddings junto;
- visão e embeddings são modelos **separados** com VRAM própria.

Consequências práticas (o que já está certo no repo e o que muda):

- ✅ **Scale-to-zero via KEDA já está** (`deploy/ai-worker-llm/deployment.yaml`,
  `replicas: 0`, trigger por lag de fila). Isso deixa de ser otimização e passa a
  ser **obrigatório**: a GPU só carrega um modelo quando há job na fila.
- ⚠️ **"Um Deployment sempre-ligado por modelo" é falso aqui.** O correto é
  **time-sharing**: os workers escalam a zero, e o vLLM carrega/descarrega
  modelo conforme o tipo de job — ou se aceita **um modelo residente** (o LLM de
  chat) e os demais (visão/embeddings) em janelas, nunca os três quentes juntos.
- ⚠️ `maxReplicaCount: 8` no worker LLM é o teto de **consumidores da fila**, não
  de instâncias de GPU: as 8 réplicas disputam **uma** vLLM. Vale documentar que
  o gargalo real é a GPU, não o pod.
- 📌 **Fallback explícito**: se a fila de IA crescer além do que uma A2000 Ada
  vaza, as opções são (a) degradar features de IA (chat responde "ocupado"), ou
  (b) rota paga pra API externa — que **quebra o "zero custo por token"** e
  precisa de decisão consciente, não silenciosa. Hoje: (a), via profundidade de
  fila + DLQ que já existem.

O RAG (chatbot) sobre 16 GB é viável e é o melhor uso da GPU; treino pesado
(camada 5) **não** roda aqui — é K8s Job agendado pra quando/onde houver mais
GPU, e na fase atual é batch pequeno ou externo.

## Faseamento

### Fase 0 — dev (é onde o repo está)
`docker-compose up` na estação: Kafka, Postgres, Valkey, MinIO, MQTT + espinha
de observabilidade. TDD como gate de CI. **Operação: zero** — é laptop.

### Fase 1 — produção de 1 site, 1–2 nós (alvo realista pra 4 pessoas)
- **Compute**: Pi (edge+core) + servidor GPU (IA). Sem Kubernetes ainda —
  `docker compose` com `restart: unless-stopped` (`deploy/raspberry/`).
- **Liga**: Gateway, Identity, Core.Execution, Edge, Telemetry.Ingest,
  Predictive, Notifications, o cliente PWA, espinha de observabilidade,
  OpenBao, Keycloak. Chatbot/IA **sob demanda** (scale-to-zero).
- **NÃO liga ainda** (dor não existe com 1 site / 4 pessoas): service mesh
  (mTLS via compose+rede fechada resolve), multi-região/MirrorMaker, Harbor
  (registry gerenciado ou GHCR + scan no CI já cobre supply chain), Pyrra
  (SLO em painel Grafana manual basta), Feast/MLflow completos (o registry de
  modelo pode ser um bucket versionado no MinIO).
- **Continuidade**: o [PITR caseiro](./continuidade-rpo-rto.md) que já existe.

### Fase 2 — Kubernetes, multi-nó, multi-região (exige mais gente)
Tudo que a arquitetura descreve como estado final: HPA/KEDA no cluster,
ArgoCD/Flux, service mesh, Harbor+cosign com admission policy, Velero,
MirrorMaker 2, Pyrra. **Gatilho pra entrar aqui**: mais de 1 site em produção,
ou a operação manual da fase 1 já consumindo tempo demais do time — não uma
data no calendário. Idealmente com uma 5ª/6ª pessoa dedicada a plataforma.

## Como não se enganar

- O erro clássico é montar a fase 2 "porque está no diagrama" e o time virar SRE
  de uma plataforma sem usuários. **A fase 1 tem que sangrar antes.**
- Cada item da [plataforma de engenharia](../arquitetura.md) tem um **gatilho de
  dor** explícito, não um cronograma. Se ninguém sente a falta, não entra.
- O custo real não é licença (tudo OSS) — é **atenção humana**. Essa é a moeda
  escassa de um time de 4, e este doc existe pra protegê-la.
