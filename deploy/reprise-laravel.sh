#!/bin/sh
# Reprise Wimo Laravel → Wimo .NET (recette aujourd'hui, production demain : même script). Sur le VPS :
#   sh app-dotnet/deploy/reprise-laravel.sh [--dry-run-only]
# depuis /opt/wimo-delivery. Enchaîne, en s'arrêtant à la première erreur :
#   1. dump Laravel frais (mysqldump --single-transaction, LECTURE SEULE) dans .runtime/laravel-reprise/ ;
#   2. CSV de CE dump, et copie MariaDB isolée de CE dump (conteneur sans réseau, en mémoire) : les contrôles
#      comparent au même instantané, pas à la production Laravel qui bouge ;
#   3. dump de la base .NET (backups/), essai à blanc (rapport-dry-run.md) ;
#   4. --apply (rapport-apply.md), contrôles d'argent : un écart → restauration du dump .NET et arrêt (code 1) ;
#   5. contrôles par échantillon, puis suppression de la copie MariaDB.
# L'outil tourne dans l'image API déployée (APP_VERSION de app-dotnet.env). Données personnelles : uniquement sous
# .runtime/laravel-reprise/ (700), jamais dans Git ni dans la sortie (identifiants Laravel, codes et montants seulement).
set -eu
cd /opt/wimo-delivery
umask 077
R=/opt/wimo-delivery/.runtime/laravel-reprise
CHECKS=app-dotnet/api/tools/LegacyImport/checks
PG_CONTAINER=wimo-delivery-dotnet-postgres-1
SNAPSHOT=reprise-laravel-snapshot
ENV=app-dotnet.env
DB=$(grep '^POSTGRES_DB=' $ENV | cut -d= -f2-)
TAG=$(grep '^APP_VERSION=' $ENV | cut -d= -f2-)
TS=$(date -u +%Y%m%d_%H%M%S)
mkdir -p $R && chmod 700 $R

echo "== 1. Dump Laravel (lecture seule)"
DUMP=$R/wimo-laravel_$TS.sql.gz
docker exec wimo-wimo-db sh -c 'exec mysqldump -u root -p"$MYSQL_ROOT_PASSWORD" --single-transaction --skip-lock-tables --no-tablespaces --default-character-set=utf8mb4 wimo' | gzip > $DUMP
zcat $DUMP | tail -1 | grep -q 'Dump completed' || { echo "dump Laravel incomplet" >&2; exit 1; }
echo "   $(basename $DUMP) ($(du -h $DUMP | cut -f1))"

echo "== 2. CSV et copie isolée de ce dump"
rm -rf $R/csv && mkdir -p $R/csv
python3 -I app-dotnet/scripts/laravel-dump-to-csv.py $DUMP $R/csv > /dev/null
docker rm -f $SNAPSHOT > /dev/null 2>&1 || true
docker run -d --name $SNAPSHOT --network none --memory 512m -e MYSQL_ROOT_PASSWORD="$(head -c 18 /dev/urandom | base64 | tr -dc A-Za-z0-9)" \
  --tmpfs /var/lib/mysql:size=1g mariadb:10.4 > /dev/null
until docker exec $SNAPSHOT sh -c 'mysql -u root -p"$MYSQL_ROOT_PASSWORD" -e "select 1"' > /dev/null 2>&1; do sleep 2; done
docker exec $SNAPSHOT sh -c 'mysql -u root -p"$MYSQL_ROOT_PASSWORD" -e "create database wimo"'
zcat $DUMP | docker exec -i $SNAPSHOT sh -c 'mysql -u root -p"$MYSQL_ROOT_PASSWORD" wimo'
echo "   $(ls $R/csv | wc -l) tables, copie $SNAPSHOT prête"

echo "== 3. Dump de $DB, essai à blanc"
BACKUP=backups/${DB}_avant-reprise-laravel_$TS.dump
docker exec $PG_CONTAINER pg_dump -U wimo -d $DB -Fc > $BACKUP
docker exec -i $PG_CONTAINER pg_restore -l < $BACKUP > /dev/null
echo "   $BACKUP ($(du -h $BACKUP | cut -f1), sha256 $(sha256sum $BACKUP | cut -c1-16))"
run() {
  docker run --rm --network wimo-delivery-dotnet_net -v $R:/data -u "$(id -u):$(id -g)" \
    -e "ConnectionStrings__Default=Host=$PG_CONTAINER;Port=5432;Database=$DB;Username=wimo;Password=$(grep '^POSTGRES_PASSWORD=' $ENV | cut -d= -f2-)" \
    --entrypoint dotnet wimo-delivery-dotnet-api:$TAG LegacyImport.dll /data/csv "$@" 2>&1 | grep -v 'libgssapi\|QuerySplitting\|^Error: libgssapi' || true
}
run --dry-run --report /data/rapport-dry-run.md
if [ "${1:-}" = "--dry-run-only" ]; then docker rm -f $SNAPSHOT > /dev/null; echo "Essai à blanc seul : $R/rapport-dry-run.md"; exit 0; fi

echo "== 4. Application, contrôles d'argent"
run --apply --batch 500 --report /data/rapport-apply.md
mkdir -p $R/controles-$TS
if ! SRC_DB=$SNAPSHOT sh $CHECKS/money-check.sh $DB $R/controles-$TS; then
  echo "ÉCART D'ARGENT : restauration de $BACKUP et arrêt" >&2
  docker exec -i $PG_CONTAINER pg_restore -U wimo -d $DB --clean --if-exists --no-owner < $BACKUP
  docker rm -f $SNAPSHOT > /dev/null
  exit 1
fi

echo "== 5. Contrôles par échantillon"
SRC_DB=$SNAPSHOT sh $CHECKS/samples.sh $DB $R/controles-$TS || echo "ÉCHANTILLON : un champ diffère (détail dans $R/controles-$TS)" >&2
docker rm -f $SNAPSHOT > /dev/null
echo "Reprise appliquée depuis $(basename $DUMP) ; sauvegarde d'avant : $BACKUP ; rapports : $R/rapport-apply.md"
