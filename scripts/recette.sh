#!/usr/bin/env bash
# Recette globale : parcours admin / vendeur / livreur joués en curl contre une pile levée
# (docker compose up -d --build, ou API locale + web local).
#
#   API=http://localhost:5080/api WEB=http://localhost:3000 \
#   ADMIN_EMAIL=admin@wimo.ma ADMIN_PASSWORD=... scripts/recette.sh
#
# Aucun raccourci en base : chaque étape passe par l'API publique, comme le web et le mobile.
# WEB vide : la partie « web » est sautée (annoncée comme telle, jamais comptée PASS).
#
# Nettoyage (--cleanup, par défaut ; --no-cleanup pour garder les données) : avec CLEANUP_PSQL (commande
# psql lisant l'entrée standard, ex. « docker exec -i wimo-delivery-dotnet-postgres-1 psql -U wimo -d wimo_dotnet »),
# tout ce que le passage a créé est supprimé en base (scripts/recette-cleanup.sql) et les compteurs de
# numérotation reprennent leur valeur d'avant. Sans CLEANUP_PSQL : comptes supprimés, fiches désactivées.
set -uo pipefail

CLEANUP=1
for arg in "$@"; do
  case "$arg" in
    --cleanup) CLEANUP=1 ;;
    --no-cleanup) CLEANUP=0 ;;
    *) echo "argument inconnu : $arg (--cleanup | --no-cleanup)" >&2; exit 2 ;;
  esac
done
CLEANUP_PSQL="${CLEANUP_PSQL:-}"
HERE="$(cd "$(dirname "$0")" && pwd)"
SINCE="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
if [ "$CLEANUP" = 1 ] && [ -n "$CLEANUP_PSQL" ]; then
  DOC_SEQ="$($CLEANUP_PSQL -qAt -c 'select coalesce(json_agg(d), $$[]$$) from document_sequences d' </dev/null)" || { echo "CLEANUP_PSQL injoignable" >&2; exit 2; }
  PARCEL_SEQ="$($CLEANUP_PSQL -qAt -c 'select coalesce(json_agg(d), $$[]$$) from parcel_code_sequences d' </dev/null)"
fi

API="${API:-http://localhost:5080/api}"
WEB="${WEB-http://localhost:3000}"
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@wimo.ma}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:?ADMIN_PASSWORD requis (celui du seed SEED_ADMIN_PASSWORD)}"
# Ville et agence : celles passées, sinon Casablanca (code CAS) et sa première agence, lues par l'API.
CITY="${CITY:-}"
AGENCY="${AGENCY:-}"
RUN="r$(date +%s)"
PASSWORD="Recette-${RUN}!"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

PASS=0
FAIL=0
BODY=""
STATUS=""

ok() { PASS=$((PASS + 1)); printf '  PASS  %s\n' "$1"; }
ko() { FAIL=$((FAIL + 1)); printf '  FAIL  %s\n' "$1"; [ -n "${2:-}" ] && printf '        %s\n' "$(printf '%s' "$2" | head -c 400)"; }
step() { printf '\n== %s\n' "$1"; }

# json '<expression JS sur j>' : lit $BODY, affiche le résultat (chaîne brute).
json() {
  printf '%s' "$BODY" | node -e '
    let s = ""; process.stdin.on("data", d => (s += d)).on("end", () => {
      let j; try { j = JSON.parse(s); } catch { process.stdout.write(""); return; }
      const v = (new Function("j", "return (" + process.argv[1] + ")"))(j);
      process.stdout.write(v === undefined || v === null ? "" : typeof v === "object" ? JSON.stringify(v) : String(v));
    });' "$1"
}

# call <libellé> <statut attendu> <verbe> <chemin> <jeton|-> [corps JSON]
call() {
  local label="$1" expect="$2" method="$3" path="$4" token="$5" data="${6:-}"
  local args=(-s -o "$TMP/body" -w '%{http_code}' -X "$method" -H 'x-client: mobile' -H 'accept: application/json')
  [ "$token" != "-" ] && args+=(-H "authorization: Bearer $token")
  # Corps passé par fichier : les octets UTF-8 arrivent intacts, y compris sous Windows.
  if [ -n "$data" ]; then printf '%s' "$data" >"$TMP/data"; args+=(-H 'content-type: application/json' --data-binary "@$TMP/data"); fi
  STATUS="$(curl "${args[@]}" "$API$path")"
  BODY="$(cat "$TMP/body")"
  if [ "$STATUS" = "$expect" ]; then ok "$label ($method $path → $STATUS)"; return 0; fi
  ko "$label ($method $path → $STATUS, attendu $expect)" "$BODY"
  return 1
}

# check <libellé> <expression JS booléenne sur j>
check() {
  if [ "$(json "$2")" = "true" ]; then ok "$1"; else ko "$1" "$BODY"; fi
}

login() {
  call "connexion $1" 200 POST /auth/login - "{\"identifier\":\"$1\",\"password\":\"$2\"}" >/dev/null || return 1
  json 'j.accessToken'
}

# Un PDF ou un classeur : statut et type de contenu.
file() {
  local label="$1" path="$2" token="$3" type="$4"
  local got
  got="$(curl -s -o /dev/null -w '%{http_code} %{content_type}' -H "authorization: Bearer $token" "$API$path")"
  case "$got" in "200 $type"*) ok "$label ($path)";; *) ko "$label ($path → $got, attendu 200 $type)";; esac
}

step "Santé"
call "API en ligne" 200 GET /health - && check "base joignable" 'j.database !== "down"'

step "Admin : référentiel, vendeur, livreur"
ADMIN="$(login "$ADMIN_EMAIL" "$ADMIN_PASSWORD")" || { ko "admin connecté"; exit 1; }
call "villes" 200 GET "/cities?pageSize=200" "$ADMIN"
[ -z "$CITY" ] && CITY="$(json "(j.items.find(c => c.code === 'CAS') || {}).id")"
check "Casablanca au référentiel" "j.items.some(c => c.id === '$CITY')"
if [ -z "$AGENCY" ]; then
  call "agences" 200 GET "/agencies?pageSize=200" "$ADMIN" && AGENCY="$(json "((j.items || j).find(a => a.cityId === '$CITY') || (j.items || j)[0] || {}).id")"
fi
MERCHANT_EMAIL="vendeur.$RUN@recette.test"
call "fiche vendeur" 201 POST /clients "$ADMIN" "{\"name\":\"Boutique $RUN\",\"phone\":\"0612345678\",\"email\":\"$MERCHANT_EMAIL\",\"cityId\":\"$CITY\"}"
CLIENT_ID="$(json 'j.id')"
call "compte vendeur" 201 POST "/clients/$CLIENT_ID/account" "$ADMIN" \
  "{\"email\":\"$MERCHANT_EMAIL\",\"firstName\":\"Nadia\",\"lastName\":\"Recette\",\"roles\":[\"MERCHANT\"],\"password\":\"$PASSWORD\"}"
MERCHANT_USER="$(json 'j.id')"
COURIER_EMAIL="livreur.$RUN@recette.test"
call "fiche livreur" 201 POST /couriers "$ADMIN" "{\"firstName\":\"Karim\",\"lastName\":\"Recette\",\"phone\":\"0622222222\",\"cityId\":\"$CITY\",\"agencyId\":\"$AGENCY\",\"fixedSalary\":0}"
COURIER_ID="$(json 'j.id')"
call "villes et tarifs du livreur" 200 PUT "/couriers/$COURIER_ID/cities" "$ADMIN" "{\"cities\":[{\"cityId\":\"$CITY\",\"pickupFee\":5,\"deliveryFee\":15}]}"
call "compte livreur" 201 POST "/couriers/$COURIER_ID/account" "$ADMIN" \
  "{\"email\":\"$COURIER_EMAIL\",\"firstName\":\"Karim\",\"lastName\":\"Recette\",\"roles\":[\"COURIER\"],\"password\":\"$PASSWORD\"}"
COURIER_USER="$(json 'j.id')"

step "Vendeur : colis et demande de ramassage"
SELLER="$(login "$MERCHANT_EMAIL" "$PASSWORD")" || ko "vendeur connecté"
call "profil vendeur" 200 GET /auth/me "$SELLER"
call "création d'un colis" 201 POST /parcels "$SELLER" \
  "{\"clientId\":\"$CLIENT_ID\",\"description\":\"Article recette\",\"recipientName\":\"Nora\",\"recipientPhone\":\"0611111111\",\"originCityId\":\"$CITY\",\"pickupAddress\":\"Dépôt\",\"destinationCityId\":\"$CITY\",\"deliveryAddress\":\"Rue 1\",\"codAmount\":250}"
PARCEL_ID="$(json 'j.id')"
PARCEL="$(json 'j.code')"
call "fiche du colis" 200 GET "/parcels/$PARCEL" "$SELLER" && check "colis au statut 1" 'j.statusId === 1'
call "demande de ramassage" 200 POST /merchant/me/pickups "$SELLER" "{\"parcelIds\":[\"$PARCEL_ID\"]}"
PICKUP="$(json 'j.code')"
call "mes colis" 200 GET /parcels "$SELLER" && check "le vendeur voit son colis" "j.items.some(p => p.code === '$PARCEL')"

step "Admin : ramassage validé, circuit de livraison"
call "validation du bon $PICKUP" 201 POST "/delivery-notes/$PICKUP/validate" "$ADMIN" "{\"parcelIds\":[\"$PARCEL_ID\"]}"
call "colis prêt" 200 GET "/parcels/$PARCEL" "$ADMIN" && check "colis au statut 3" 'j.statusId === 3'
call "circuit" 201 POST /routes "$ADMIN" "{\"cityId\":\"$CITY\",\"courierId\":\"$COURIER_ID\"}"
ROUTE="$(json 'j.code')"
call "colis au circuit" 201 POST "/routes/$ROUTE/parcels" "$ADMIN" "{\"parcelIds\":[\"$PARCEL_ID\"]}"
call "départ du circuit" 201 POST "/routes/$ROUTE/dispatch" "$ADMIN"
file "étiquette PDF" "/parcels/$PARCEL/label" "$ADMIN" application/pdf

step "Livreur : tournée, livraison, remise de caisse"
PHONE="$(login "$COURIER_EMAIL" "$PASSWORD")" || ko "livreur connecté"
call "tournées du livreur" 200 GET /courier/me/routes "$PHONE" && check "le circuit est affecté" "JSON.stringify(j).includes('$ROUTE')"
# Avant la livraison : son dernier colis livré clôt le circuit, et Speedy masque alors l'affectation.
call "notifications du livreur" 200 GET /me/notification-feed "$PHONE" && check "affectation notifiée" "j.items.some(n => n.topic === 'route-assigned')"
call "livraison" 200 POST "/courier/parcels/$PARCEL/status" "$PHONE" '{"statusId":4}'
call "caisse du livreur" 200 GET /courier/me/remittance "$PHONE"
REMITTANCE="$(json 'j.remittance && j.remittance.code')"
check "COD attendu de 250 MAD" 'j.remittance && j.remittance.expectedCod === 25000'

step "Admin : caisse, facture, tableau de bord"
call "confirmation de la caisse $REMITTANCE" 201 POST "/remittances/$REMITTANCE/confirm" "$ADMIN"
# Le frais du bon de ramassage a déjà ouvert le brouillon du vendeur : on le reprend.
call "facture brouillon déjà ouverte" 409 POST /invoices "$ADMIN" "{\"clientId\":\"$CLIENT_ID\",\"agencyId\":\"$AGENCY\"}"
INVOICE="$(json 'j.details && j.details.invoiceCode')"
call "colis facturé" 201 POST "/invoices/$INVOICE/parcels" "$ADMIN" "{\"parcelIds\":[\"$PARCEL_ID\"]}"
call "émission de $INVOICE" 201 POST "/invoices/$INVOICE/issue" "$ADMIN" '{}' && check "facture émise" 'j.status === "issued"'
file "PDF de la facture" "/invoices/$INVOICE/pdf" "$ADMIN" application/pdf
call "tableau de bord" 200 GET "/dashboard?period=today&agencyId=$AGENCY" "$ADMIN" && check "une livraison du jour" 'j.kpis.delivered >= 1'
call "tableau de bord finance" 200 GET "/dashboard/finance?period=today" "$ADMIN"
call "type de réclamation" 201 POST /claims/types "$ADMIN" "{\"code\":\"recette-$RUN\",\"label\":\"Recette\",\"subjectType\":\"parcel\"}"
CLAIM_TYPE="$(json 'j.id')"

step "Vendeur : facture, notifications, réclamation"
call "ma facture" 200 GET "/invoices/$INVOICE" "$SELLER"
call "mes notifications" 200 GET /me/notification-feed "$SELLER" && check "facture notifiée" "j.items.some(n => n.topic === 'invoice-issued')"
call "réclamation" 201 POST /claims "$SELLER" "{\"typeId\":\"$CLAIM_TYPE\",\"subject\":\"Retard\",\"message\":\"Le client signale un retard.\",\"parcelCode\":\"$PARCEL\"}"
CLAIM="$(json 'j.code')"
call "réponse de l'agence" 201 POST "/claims/$CLAIM/events" "$ADMIN" '{"body":"Traité.","status":"resolved"}' && check "réclamation résolue" 'j.status === "resolved"'
call "portée : factures invisibles au livreur" 200 GET /invoices "$PHONE" && check "aucune facture" 'j.total === 0'
call "portée : le vendeur n'ouvre pas le tableau de bord" 403 GET /dashboard "$SELLER"

step "Web Speedy branché sur l'API .NET"
if [ -z "$WEB" ]; then
  printf '  SKIP  WEB vide : pages web non jouées\n'
else
  code="$(curl -s -o /dev/null -w '%{http_code}' "$WEB/healthz.txt")"
  [ "$code" = 200 ] && ok "web en ligne ($code)" || ko "web en ligne ($WEB/healthz.txt → $code)"
  code="$(curl -s -o /dev/null -w '%{http_code}' "$WEB/fr/login")"
  [ "$code" = 200 ] && ok "page de connexion ($code)" || ko "page de connexion ($WEB/fr/login → $code)"
  code="$(curl -s -o "$TMP/home" -w '%{http_code}' "$WEB/fr")"
  [ "$code" = 200 ] && ok "accueil public rendu par le serveur ($code)" || ko "accueil public ($WEB/fr → $code)"
  # Session navigateur : les cookies httpOnly `dp.at` / `dp.rt` que le serveur Next relit. Passés en en-tête,
  # ils valent pour toute paire d'hôtes (localhost:3000 / :5080 comme new / api-new.wimodelivery.ma).
  for who in "admin:$ADMIN_EMAIL:$ADMIN_PASSWORD:/fr/dashboard" "vendeur:$MERCHANT_EMAIL:$PASSWORD:/fr/parcels" "livreur:$COURIER_EMAIL:$PASSWORD:/fr/me"; do
    IFS=: read -r name email password page <<<"$who"
    call "session web $name" 200 POST /auth/login - "{\"identifier\":\"$email\",\"password\":\"$password\"}" >/dev/null || continue
    cookie="dp.at=$(json 'j.accessToken'); dp.rt=$(json 'j.refreshToken')"
    code="$(curl -s -H "Cookie: $cookie" -o "$TMP/$name.html" -w '%{http_code} %{redirect_url}' "$WEB$page")"
    case "$code" in 200*) ok "web $name : $page rendu ($code)";; *) ko "web $name : $page → $code";; esac
    if [ "$name" = livreur ]; then
      # Le livreur n'a pas de back-office : toute page d'administration le renvoie vers son espace.
      code="$(curl -s -H "Cookie: $cookie" -o /dev/null -w '%{http_code} %{redirect_url}' "$WEB/fr/dashboard")"
      case "$code" in 30[0-9]*"/fr/me"*) ok "web livreur : /fr/dashboard renvoie vers son espace ($code)";; *) ko "web livreur : /fr/dashboard → $code (attendu une redirection vers /fr/me)";; esac
    fi
  done
fi

if [ "$CLEANUP" = 0 ]; then
  step "Nettoyage : désactivé (--no-cleanup)"
  echo "Données gardées : vendeur « Boutique $RUN », colis ${PARCEL:-}, facture ${INVOICE:-}, réclamation ${CLAIM:-}."
elif [ -n "$CLEANUP_PSQL" ]; then
  step "Nettoyage : tout ce que la recette a créé est supprimé en base"
  out="$($CLEANUP_PSQL -qAt -F' | ' -v client="${CLIENT_ID:-}" -v courier="${COURIER_ID:-}" -v merchant_user="${MERCHANT_USER:-}" \
    -v courier_user="${COURIER_USER:-}" -v claim_type="${CLAIM_TYPE:-}" -v since="$SINCE" -v doc_seq="$DOC_SEQ" -v parcel_seq="$PARCEL_SEQ" \
    <"$HERE/recette-cleanup.sql" 2>&1)" && ok "suppression en base ($(printf '%s' "$out" | tail -1))" || ko "suppression en base" "$out"
  [ -n "${PARCEL:-}" ] && call "le colis de recette n'existe plus" 404 GET "/parcels/$PARCEL" "$ADMIN"
  [ -n "${INVOICE:-}" ] && call "la facture de recette n'existe plus" 404 GET "/invoices/$INVOICE" "$ADMIN"
  [ -n "${CLAIM:-}" ] && call "la réclamation de recette n'existe plus" 404 GET "/claims/$CLAIM" "$ADMIN"
  [ -n "${CLIENT_ID:-}" ] && call "la fiche vendeur n'existe plus" 404 GET "/clients/$CLIENT_ID" "$ADMIN"
else
  step "Nettoyage : comptes de recette supprimés, fiches désactivées"
  # Sans accès à la base, l'API refuse de supprimer un colis livré ou une facture émise : ils restent,
  # rattachés à « Boutique $RUN » / « Karim Recette ».
  [ -n "${MERCHANT_USER:-}" ] && call "compte vendeur supprimé" 200 DELETE "/users/$MERCHANT_USER" "$ADMIN"
  [ -n "${COURIER_USER:-}" ] && call "compte livreur supprimé" 200 DELETE "/users/$COURIER_USER" "$ADMIN"
  [ -n "${CLIENT_ID:-}" ] && call "fiche vendeur désactivée" 200 PATCH "/clients/$CLIENT_ID/active" "$ADMIN" '{"isActive":false}'
  [ -n "${COURIER_ID:-}" ] && call "fiche livreur désactivée" 200 PATCH "/couriers/$COURIER_ID/active" "$ADMIN" '{"isActive":false}'
  [ -n "${CLAIM_TYPE:-}" ] && call "type de réclamation désactivé" 200 PATCH "/claims/types/$CLAIM_TYPE" "$ADMIN" \
    "{\"code\":\"recette-$RUN\",\"label\":\"Recette\",\"subjectType\":\"parcel\",\"isActive\":false}"
  echo "Traces laissées : vendeur « Boutique $RUN », colis ${PARCEL:-}, facture ${INVOICE:-}, réclamation ${CLAIM:-}."
fi
if [ "$CLEANUP" = 1 ]; then
  code="$(curl -s -o /dev/null -w '%{http_code}' -H 'x-client: mobile' -H 'content-type: application/json' \
    --data "{\"identifier\":\"$MERCHANT_EMAIL\",\"password\":\"$PASSWORD\"}" "$API/auth/login")"
  [ "$code" != 200 ] && ok "le compte vendeur de recette ne se connecte plus ($code)" || ko "le compte vendeur de recette se connecte encore"
fi

printf '\n%s PASS, %s FAIL\n' "$PASS" "$FAIL"
if [ "$FAIL" -eq 0 ]; then echo "RECETTE PASS"; exit 0; fi
echo "RECETTE FAIL"
exit 1
