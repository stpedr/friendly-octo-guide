# DR: região standby e failover

Ativo-passivo: o site primário opera; o standby recebe **dados** continuamente
e fica pronto pra assumir. Os objetivos (RPO/RTO por sistema) estão em
`docs/governanca/continuidade-rpo-rto.md` — este runbook é o "como".

## O que replica continuamente

| Camada | Mecanismo | Config |
|---|---|---|
| Kafka (eventos/alertas/auditoria) | MirrorMaker 2, primário → standby | `deploy/dr/mm2.properties` |
| Postgres | WAL archiving + dump diário (e réplica streaming no perfil `ha`) | `docker-compose.yml` + `deploy/infra/` |
| Cluster K8s (objetos + volumes) | Velero → MinIO | `deploy/dr/velero-schedule.yaml` |
| Data lake | replicação de bucket MinIO (`mc mirror --watch primario/linha-lake standby/linha-lake`) | operação |

## Runbook de failover (primário morreu)

1. **Declare o desastre** — decisão humana, com hora registrada. Nada de
   failover automático em plataforma com atuador físico.
2. **Postgres**: promova a réplica (`pg_ctl promote`) ou restaure dump + WAL
   (procedimento em `docs/governanca/continuidade-rpo-rto.md`).
3. **Kafka**: os tópicos replicados existem no standby com prefixo
   `primario.` (convenção MM2). Aponte os consumers pro standby; o
   `sync.group.offsets` já traduziu os offsets de grupo.
4. **Plataforma**: suba os serviços no standby apontando pros novos hosts
   (compose: trocar env; cluster: `kubectl apply` dos mesmos manifests, ou
   `velero restore create --from-backup <ultimo>`).
5. **Borda**: reaponte os edge gateways (`Mqtt__Host`/`Kafka__Bootstrap`).
   O store-and-forward deles segurou o buraco — é pra isso que ele existe.
6. **DNS/clientes**: troque o endereço do Gateway (o PWA/Tauri leem a URL de
   config, não hardcode).

## Failback

Não corra: replique de volta (MM2 no sentido inverso + dump do Postgres),
faça a troca num momento de linha parada e SÓ ENTÃO religue o sentido normal.

## Game day

Trimestral: derrube o primário de propósito e cronometre o RTO real. DR sem
ensaio geral é documento, não capacidade.
