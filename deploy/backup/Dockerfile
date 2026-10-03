FROM postgres:16-alpine
RUN apk add --no-cache rclone tzdata
COPY backup.sh restore.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/backup.sh /usr/local/bin/restore.sh
# Scheduler mínimo: una copia por día a la hora BACKUP_HOUR_UTC (revisa cada 5 minutos).
CMD ["/bin/sh", "-c", "while true; do if [ \"$(date -u +%H)\" = \"$(printf %02d ${BACKUP_HOUR_UTC:-6})\" ] && [ ! -f /backups/.done-$(date -u +%Y%m%d) ]; then backup.sh && touch /backups/.done-$(date -u +%Y%m%d); fi; sleep 300; done"]
