# Continuidade: RPO/RTO, backup e restore

## Objetivos formais

| Sistema | RPO (perda máxima) | RTO (indisponibilidade máxima) |
|---|---|---|
| Postgres (ordens, identidade, knowledge) | **≤ 5 min** (WAL archiving contínuo) | **≤ 1 h** |
| Kafka (tópicos de evento) | ≤ 24 h — reconstruível: edge faz store-and-forward e o lake guarda tudo | ≤ 1 h |
| Data lake (MinIO) | ≤ 24 h | ≤ 4 h (não bloqueia operação) |
| Observabilidade (Loki/Tempo/VM) | melhor esforço — perder métrica não para a linha | ≤ 8 h |

Racional: a linha física continua produzindo mesmo com a plataforma fora —
o edge (`Edge.ProtocolGateway`) bufferiza e reenvia. O que NÃO pode se perder
é a verdade transacional (ordens) e a identidade.

## Como o RPO é cumprido (o que já existe no repo)

1. **WAL archiving** — o primário roda com `archive_mode=on` copiando cada
   segmento pro volume `wal-archive` (ver `docker-compose.yml`). Perda máxima
   = 1 segmento não arquivado (~16 MB ou o intervalo de escrita, o que for menor).
2. **Dump lógico diário** com retenção de 14 dias — serviço `pg-backup`
   (perfil `ha`), script em `deploy/infra/pg-backup.sh`. O dump só aparece no
   diretório quando completo (escrita em `.tmp` + rename).
3. **Réplica de leitura por streaming** — serviço `postgres-replica` (perfil
   `ha`), clonada com `pg_basebackup` + slot (`deploy/infra/postgres-replica-entrypoint.sh`).
   Além de aliviar leitura, é um candidato a promoção em desastre do primário.

## Procedimento de restore (PITR caseiro)

Testado a cada mudança relevante no esquema — restore que nunca rodou é
loteria, não backup:

```bash
# 1. Pare os serviços que escrevem
docker compose stop core-execution identity telemetry-ingest knowledge

# 2. Restaure o dump mais recente num volume novo
gunzip -c backups/<mais-recente>.sql.gz | psql -h <novo-postgres> -U dev postgres

# 3. (PITR) Copie o wal-archive pro novo nó e configure recovery até o instante desejado
#    restore_command = 'cp /wal-archive/%f %p'
#    recovery_target_time = '2026-07-13 14:00:00+00'

# 4. Aponte os serviços (ConnectionStrings__Postgres) e suba
docker compose up -d
```

Promoção da réplica (alternativa mais rápida quando o primário morreu mas a
réplica está sã): `pg_ctl promote` na réplica e trocar o host nas conn strings.

## O que falta pra fase 2 (K8s multi-nó)

- Backup pra fora da máquina (hoje volume local; mover pro MinIO/S3 com `mc mirror` ou Velero no cluster).
- DR multi-região com réplica assíncrona remota.
- Game day trimestral: derrubar o primário de propósito e cronometrar o RTO real.
