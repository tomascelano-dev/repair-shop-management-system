#!/bin/sh
# Restaura un dump sobre la base actual. Primero frená la API:
#   docker compose stop api
#   docker compose exec backup restore.sh /backups/db-AAAAMMDD-HHMMSS.dump
#   docker compose start api
# Los archivos se restauran aparte: tar -xzf /backups/files-....tar.gz -C <volumen app_data>.
set -eu
file="${1:?Uso: restore.sh /backups/db-XXXX.dump}"
pg_restore --clean --if-exists --no-owner -d "$PGDATABASE" "$file"
echo "Restaurado $file"
