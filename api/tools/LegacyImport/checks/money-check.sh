#!/bin/sh
# Contrôles d'argent de la reprise Wimo Laravel : source.sql (MariaDB Laravel, lecture seule) contre target.sql (base .NET).
#   sh money-check.sh <base-postgres> <dossier-sortie>
# Sortie : PASS (zéro écart) ou les lignes qui diffèrent (code 1). Les deux relevés restent dans <dossier-sortie>.
set -eu
DB=${1:?base PostgreSQL}
OUT=${2:?dossier de sortie}
HERE=$(dirname "$0")
docker exec -i wimo-wimo-db sh -c 'exec mysql -u root -p"$MYSQL_ROOT_PASSWORD" -N -B --default-character-set=utf8mb4 wimo' \
  < "$HERE/source.sql" | sort > "$OUT/controle-source.txt"
docker exec -i wimo-delivery-dotnet-postgres-1 psql -q -U wimo -d "$DB" -v ON_ERROR_STOP=1 \
  < "$HERE/target.sql" | grep -v '^$' | sort > "$OUT/controle-cible.txt"
if diff "$OUT/controle-source.txt" "$OUT/controle-cible.txt" > "$OUT/controle-ecarts.txt"; then
  echo "PASS : $(wc -l < "$OUT/controle-source.txt") lignes de contrôle identiques (factures, règlements et colis par client ; recettes et factures par livreur ; colis par statut)"
else
  echo "ÉCARTS ($(grep -c '^[<>]' "$OUT/controle-ecarts.txt") lignes ; < source, > cible) :"
  cat "$OUT/controle-ecarts.txt"
  exit 1
fi
