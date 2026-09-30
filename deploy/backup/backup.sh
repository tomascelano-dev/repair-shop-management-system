#!/bin/sh
# Daily backup: database (custom format) and app data (photos + encryption keys).
set -eu
stamp=$(date -u +%Y%m%d-%H%M%S)
mkdir -p /backups
pg_dump -Fc -f "/backups/db-$stamp.dump"
tar -czf "/backups/data-$stamp.tar.gz" -C /app data 2>/dev/null || tar -czf "/backups/data-$stamp.tar.gz" -C / app/data
# Verify the dump can be read before counting it as a backup.
pg_restore --list "/backups/db-$stamp.dump" > /dev/null
find /backups -name 'db-*.dump' -mtime +"${BACKUP_KEEP_DAYS:-14}" -delete
find /backups -name 'data-*.tar.gz' -mtime +"${BACKUP_KEEP_DAYS:-14}" -delete
find /backups -name '.done-*' -mtime +3 -delete
if [ -n "${RCLONE_REMOTE:-}" ]; then
  rclone copy /backups "$RCLONE_REMOTE" --include "db-$stamp.dump" --include "data-$stamp.tar.gz"
fi
echo "Backup $stamp OK"
