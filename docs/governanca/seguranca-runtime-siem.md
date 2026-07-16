# Segurança de runtime e SIEM

**Status: definido em 2026-07 — detecção em runtime (Falco) e correlação de
eventos de segurança são um funil SEPARADO da observabilidade operacional.**

## A lacuna

A supply chain já é forte: Trivy escaneia imagem, cosign assina, SBOM, o cluster
recusa imagem não assinada (admission policy). A borda tem WAF (ModSecurity/CRS).
Os segredos estão no OpenBao. **Mas tudo isso é preventivo e de build-time.**
Falta o que acontece **depois** que um container já está rodando:

1. **Detecção em runtime** — um processo inesperado, um shell dentro de um pod,
   escrita num caminho sensível, conexão de saída que não devia existir.
2. **Correlação de eventos de segurança** — hoje log/trace/métrica vão pra
   espinha *operacional* (Loki/Tempo/VM). Evento de segurança tem público,
   retenção e workflow diferentes; misturar os dois esconde o sinal.
3. **Resposta a *breach*** — o `Alertmanager → ntfy` cobre incidente
   **operacional** (linha parou, latência subiu). Incidente de **segurança**
   (credencial vazou, container comprometido) tem outra cadeia de decisão.

## Por que separar do funil de observabilidade

| | Observabilidade operacional | Segurança |
|---|---|---|
| Pergunta | "a linha está saudável?" | "alguém está agindo fora do esperado?" |
| Público | on-call de produto | resposta a incidente / segurança |
| Retenção | 6–13 meses (ver PII/LGPD) | mais longa, imutável, pra forense |
| Se cair | perde-se métrica, linha produz | perde-se a trilha de um ataque |

Misturar os dois faz o alerta de segurança competir com ruído operacional e
herdar a retenção curta — exatamente quando você mais precisa do histórico.
Por isso: **mesmo OTel Collector como coletor, pipeline e destino distintos.**

## O desenho

```
   Falco (DaemonSet, cada nó) ──┐
   audit log admin (Identity) ──┼─► OTel Collector ─► pipeline "seguranca" ─► store append-only (MinIO WORM)
   decisões do Decision Engine ─┤       (mesma coleta)   (separado do de obs)      + alerta -> canal de SECURITY
   admission denies (cosign)  ──┘
```

- **Falco** (`deploy/security/falco/`): sensor de runtime baseado em eBPF/syscall.
  Regras específicas desta plataforma, não só as default — ver o arquivo de
  regras. Roda como **DaemonSet** na fase 2 (K8s); na fase 1 (compose no Pi) roda
  como serviço host, com o subconjunto de regras que faz sentido fora de K8s.
- **Fontes correlacionadas**: além do Falco, entra a **trilha de auditoria
  administrativa** (`schemas/auditoria-admin.avsc` + `Platform.Audit` — ver a
  seção abaixo), as decisões auditadas do
  `Decision.Engine` (`schemas/decisao-auditada.avsc`) e as negações de admission
  do cosign.
- **Destino**: bucket **WORM** no MinIO (o mesmo mecanismo de imutabilidade do
  Big Data Pool), retenção longa, separado dos índices de Loki. É o que dá
  forense — um atacante não apaga a própria trilha.
- **Alerta**: evento crítico de segurança sai por um **canal ntfy dedicado**
  (não o de on-call operacional) com runbook de resposta a incidente próprio.

## Regras que importam nesta plataforma (além das default)

O `deploy/security/falco/rules-plataforma.yaml` cobre o que é específico daqui:

- **Shell/exec dentro de qualquer pod de serviço** — os serviços .NET nunca
  precisam de shell interativo; um `sh`/`bash` num pod é sinal, não rotina.
- **Saída de rede a partir de pod voltado pro OT** — o fluxo default é
  unidirecional OT→IT (modelo Purdue); conexão de saída inesperada do
  `edge-protocol-gateway` é exatamente o que o isolamento existe pra impedir.
- **Escrita em binário/lib de container** — imagem é imutável; escrita em
  `/usr/bin`, `/lib` etc. é adulteração.
- **Leitura de arquivo de segredo fora do processo dono** — os segredos vêm do
  OpenBao pra memória do serviço; acesso ao mount por outro processo é suspeito.

## Trilha de auditoria administrativa

Ações administrativas sensíveis (mudança de permissão/atributo, revogação de
certificado de dispositivo, override de decisão) precisam de uma trilha
**append-only e imutável**, separada do log de acesso operacional — que tem
retenção curta (6 meses, LGPD/Marco Civil) e não é forense.

**Contrato**: `schemas/auditoria-admin.avsc` (`auditoria.admin.v1`) — ator,
papéis do ator, ação, alvo, before/after **já redigidos**, trace-id, timestamp.

**Núcleo reusável**: `src/Platform/Platform.Audit/` — todo emissor (Identity,
edge, decision-engine) constrói o mesmo evento pelo mesmo caminho, com a mesma
**redação por nome de campo** (`AuditRedaction`): senha, seed TOTP, segredo e
token nunca entram na trilha, nem no before nem no after. A trilha prova *que*
uma permissão mudou; jamais expõe o valor sensível.

**Emissão**: o Identity emite em toda mudança de permissão
(`POST /v1/admin/users/{username}/permissions` → `IAdminAuditTrail`). O ator vem
sempre do token validado, nunca do corpo — auditar com ator forjável não audita
nada. O sink é escolhido por configuração, **sem mudar a interface**:
- **sem Postgres** (dev local puro): `LoggingAdminAuditTrail` (log estruturado);
- **com Postgres** (`ConnectionStrings:Postgres`): `PostgresAdminAuditTrail` grava
  o evento **durável no outbox** (`admin_audit_outbox`) antes de responder, e o
  `AdminAuditOutboxRelay` drena pro Kafka (`auditoria.admin.v1`) com backoff. Como
  o store de usuários da prod é externo (Keycloak), não há transação a
  compartilhar — durável-antes-do-ack é o equivalente correto à "mesma transação"
  do outbox do Core.Execution.

**Retenção WORM (distinta do log de acesso)**: o tópico `auditoria.admin.v1` é
consumido por uma **segunda instância do `Data.Archiver`**
(`deploy/data-archiver-audit/`) apontada pra um bucket próprio:

```
# data-archiver-audit — mesma imagem, config distinta:
Kafka__TelemetryTopic: auditoria.admin.v1
S3__Bucket:            linha-audit          # bucket separado do lake de telemetria
Archiver__WormRetentionDays: "2555"         # ~7 anos, Object Lock em modo Compliance
```

O `S3ObjectStore` já aplica `ObjectLockMode.Compliance` quando a retenção é > 0 —
nem o admin apaga antes da data. Assim a trilha herda imutabilidade e retenção
longa **sem** herdar a retenção curta do Loki.

## Fases

- **Fase 1 (Pi + compose)**: Falco host-level com o subconjunto de regras
  aplicável fora de K8s; alertas no canal de segurança. Sem SIEM completo —
  o WORM no MinIO + retenção já dá a trilha forense.
- **Fase 2 (K8s)**: Falco como DaemonSet, admission denies e audit log do
  Kubernetes na mesma correlação, e a decisão consciente de adotar (ou não) um
  SIEM OSS dedicado (ex.: Wazuh) — só se o volume de eventos justificar a
  operação extra, seguindo a régua do [doc de carga operacional](./carga-operacional.md).

## O que falta

- **Outros emissores**: hoje só a mudança de permissão do Identity emite trilha.
  Falta instrumentar a **revogação de certificado no edge**
  (`AdminAction.DeviceCertRevoked`) e o **override no Decision Engine**
  (`AdminAction.DecisionOverride`) com o mesmo `Platform.Audit`.
- **Bucket `linha-audit` com Object Lock**: o `data-archiver-audit` exige o bucket
  criado com Object Lock habilitado (provisionamento de infra, não de app).
- Plano escrito de resposta a incidente de segurança (quem é acionado, como se
  contém, comunicação) — o análogo de segurança do runbook operacional.
