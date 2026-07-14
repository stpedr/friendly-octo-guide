#!/bin/bash
set -euo pipefail

chown -R postgres:postgres /wal-archive
exec docker-entrypoint.sh "$@"
