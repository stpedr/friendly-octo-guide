#!/bin/bash
# Réplica de leitura por streaming replication (imagem oficial, sem fork):
# primeiro boot clona o primário com pg_basebackup (-R já escreve standby.signal
# + primary_conninfo); boots seguintes só sobem o standby.
set -euo pipefail

if [ ! -s "$PGDATA/PG_VERSION" ]; then
  echo "réplica vazia — clonando o primário ${PRIMARY_HOST}..."
  until pg_basebackup -h "$PRIMARY_HOST" -U replicator -D "$PGDATA" -R -X stream -C -S replica_slot; do
    echo "primário ainda não aceita replicação; tentando de novo em 3s"
    sleep 3
  done
  chmod 700 "$PGDATA"
fi

exec docker-entrypoint.sh postgres
