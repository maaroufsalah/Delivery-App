#!/usr/bin/env bash
# Import idempotent du référentiel Wimo (zones, villes + nom arabe, quartiers, agences,
# tarifs par mode de livraison) depuis un dossier de CSV — voir ReferentialImporter.cs.
#
#   scripts/import-referential.sh <dossier-csv>                 # depuis les sources (dotnet run)
#   docker compose run --rm -v "$PWD/referential:/data" api import-referential /data   # dans la pile
#
# Au déploiement, le dossier est produit par un export des seules tables de référentiel
# de la base Postgres de Wimo-Delivery (cf. docs/chantiers/delivery-app-dotnet.md).
set -euo pipefail
folder="${1:?usage: scripts/import-referential.sh <dossier-csv>}"
cd "$(dirname "$0")/../api"
dotnet run --project src/Delivery.Api -- import-referential "$(cd "$OLDPWD" && realpath "$folder")"
