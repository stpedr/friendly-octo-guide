#!/bin/bash
set -euo pipefail

cat >> "$PGDATA/pg_hba.conf" <<'HBA'
host replication replicator all scram-sha-256
HBA
