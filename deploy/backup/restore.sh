#!/bin/sh
# Restores a database dump over the current database. Stop the api service first:
#   docker compose stop api
#   docker compose exec backup restore.sh /backups/db-YYYYMMDD-HHMMSS.dump
#   docker compose start api
set -eu
file="${1:?Usage: restore.sh /backups/db-XXXX.dump}"
pg_restore --clean --if-exists --no-owner -d "$PGDATABASE" "$file"
echo "Restored $file"
