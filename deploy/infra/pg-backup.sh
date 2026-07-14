#!/bin/bash
# Backup lógico diário + retenção. Junto com o WAL archiving do primário
# (archive_command → volume wal-archive), dá PITR caseiro: restaura o dump
# mais próximo e reaplica WAL até o instante desejado.
# RPO alvo: <= 24h pelo dump, <= segundos com os WALs. RTO: minutos (ver docs/governanca).
set -euo pipefail

RETENTION_DAYS="${RETENTION_DAYS:-14}"
INTERVAL_SECONDS="${INTERVAL_SECONDS:-86400}"

while true; do
  stamp="$(date -u +%Y-%m-%dT%H%M%SZ)"
  echo "backup ${stamp} iniciando"
  pg_dumpall -h "$PGHOST" -U "$PGUSER" --no-password | gzip > "/backups/${stamp}.sql.gz.tmp"
  mv "/backups/${stamp}.sql.gz.tmp" "/backups/${stamp}.sql.gz"   # só aparece completo
  find /backups -name '*.sql.gz' -mtime "+${RETENTION_DAYS}" -delete
  echo "backup ${stamp} ok; próximo em ${INTERVAL_SECONDS}s"
  sleep "$INTERVAL_SECONDS"
done
