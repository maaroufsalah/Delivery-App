#!/usr/bin/env bash
# Sauvegarde quotidienne de la base Wimo-Delivery .NET (appelée à 03h30 par /etc/cron.d/wimo-delivery-backup,
# via /opt/wimo-delivery/backup.sh). Dump pg_dump -Fc vérifié par pg_restore -l, archive des pièces jointes,
# rotation 14 jours. Même mécanisme que la sauvegarde de la pile NestJS.
set -euo pipefail

STACK_DIR="/opt/wimo-delivery"
ENV_FILE="${STACK_DIR}/app-dotnet.env"
DB_CONTAINER="wimo-delivery-dotnet-postgres-1"
UPLOADS_VOLUME="wimo-delivery-dotnet-uploads-data"
BACKUP_DIR="${STACK_DIR}/backups"
LOG="${BACKUP_DIR}/backup-dotnet.log"
RETENTION=14
TAR_IMAGE="alpine:3.20"

set -a; . "$ENV_FILE"; set +a
DB="${POSTGRES_DB}"
STAMP="$(date +%Y%m%d_%H%M%S)"
DUMP="${BACKUP_DIR}/${DB}_${STAMP}.dump"
UPLOADS="${BACKUP_DIR}/${DB}_uploads_${STAMP}.tar.gz"

mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"
log() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*" | tee -a "$LOG"; }
fail() { log "ECHEC : $*"; exit 1; }

log "=== Debut sauvegarde ${DB} ==="
docker inspect -f '{{.State.Running}}' "$DB_CONTAINER" 2>/dev/null | grep -q true || fail "conteneur ${DB_CONTAINER} arrete"
if docker exec "$DB_CONTAINER" pg_dump -U "$POSTGRES_USER" -Fc -d "$DB" >"$DUMP" 2>>"$LOG"; then
  log "dump base OK      : $(basename "$DUMP") ($(du -h "$DUMP" | cut -f1))"
else
  rm -f "$DUMP"
  fail "pg_dump a echoue"
fi
[ -s "$DUMP" ] || fail "dump vide"
docker exec -i "$DB_CONTAINER" pg_restore -l >/dev/null <"$DUMP" 2>>"$LOG" || fail "dump illisible par pg_restore"
chmod 600 "$DUMP"

if docker volume inspect "$UPLOADS_VOLUME" >/dev/null 2>&1; then
  docker run --rm -v "${UPLOADS_VOLUME}:/data:ro" -v "${BACKUP_DIR}:/backup" "$TAR_IMAGE" \
    sh -c "tar czf '/backup/$(basename "$UPLOADS")' -C /data . && chmod 600 '/backup/$(basename "$UPLOADS")'" 2>>"$LOG" \
    || { rm -f "$UPLOADS"; fail "archivage des pieces jointes a echoue"; }
  log "pieces jointes OK : $(basename "$UPLOADS") ($(du -h "$UPLOADS" | cut -f1))"
fi

DELETED=$(find "$BACKUP_DIR" -maxdepth 1 -name "${DB}_*" -type f -mtime "+${RETENTION}" -print -delete | wc -l)
log "rotation          : ${DELETED} fichier(s) de plus de ${RETENTION} jours supprime(s)"
log "=== Fin : $(find "$BACKUP_DIR" -maxdepth 1 -name "${DB}_*.dump" -type f | wc -l) dump(s), $(du -sh "$BACKUP_DIR" | cut -f1) au total ==="
