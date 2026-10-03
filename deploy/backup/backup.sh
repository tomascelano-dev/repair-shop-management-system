#!/bin/sh
# Copia diaria: base de datos (formato custom de pg_dump) y archivos subidos (fotos, firmas, logos).
# También se puede correr a mano: docker compose exec backup backup.sh
set -eu
out="${BACKUP_DIR:-/backups}"
data="${DATA_DIR:-/app/data}"
stamp=$(date -u +%Y%m%d-%H%M%S)
mkdir -p "$out"
pg_dump -Fc -f "$out/db-$stamp.dump"
# Verifica que el dump se pueda leer antes de darlo por bueno.
pg_restore --list "$out/db-$stamp.dump" > /dev/null
tar -czf "$out/files-$stamp.tar.gz" -C "$data" uploads
find "$out" -name 'db-*.dump' -mtime +"${BACKUP_KEEP_DAYS:-14}" -delete
find "$out" -name 'files-*.tar.gz' -mtime +"${BACKUP_KEEP_DAYS:-14}" -delete
find "$out" -name '.done-*' -mtime +3 -delete
if [ -n "${RCLONE_REMOTE:-}" ]; then
  rclone copy "$out" "$RCLONE_REMOTE" --include "db-$stamp.dump" --include "files-$stamp.tar.gz"
fi
echo "Backup $stamp OK"
