#!/usr/bin/env bash
# Recette de la reprise Wimo Laravel, par l'API (aucun INSERT) sur des comptes et des données importés :
#   1. connexion d'un vendeur et d'un livreur importés (mot de passe de test posé par l'API, empreinte d'origine remise) ;
#   2. le vendeur voit ses colis et ses factures historiques, montants identiques à Laravel ;
#   3. nouveau colis du vendeur : frais = tarif de sa ville avec sa remise (règle Laravel calculeFraisLivraison) ;
#   4. circuit avec le livreur importé, livraison, caisse RF-, puis facture brouillon sur des colis importés non
#      facturés : montants = formule Laravel ;
#   5. une facture, un règlement et une caisse importés ne se modifient pas.
# Tout ce que le passage crée est supprimé à la fin (scripts/recette-reprise-cleanup.sql), compteurs restaurés.
#
#   API=https://api-new.wimodelivery.ma/api ADMIN_EMAIL=… ADMIN_PASSWORD=… \
#   PSQL="docker exec -i wimo-delivery-dotnet-postgres-1 psql -U wimo -d wimo_dotnet" bash scripts/recette-reprise.sh
#
# MYSQL (lecture seule de Wimo Laravel) : par défaut le client mysql du conteneur wimo-wimo-db. Aucune donnée
# personnelle n'est affichée : seulement des identifiants Laravel, des codes et des montants.
set -uo pipefail

API="${API:?API requis}"
ADMIN_EMAIL="${ADMIN_EMAIL:?ADMIN_EMAIL requis}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:?ADMIN_PASSWORD requis}"
PSQL="${PSQL:?PSQL requis : psql de la base .NET, lisant stdin}"
HERE="$(cd "$(dirname "$0")" && pwd)"
SINCE="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
RUN="rr$(date +%s)"
TMP="$(mktemp -d)"
chmod 700 "$TMP"
trap 'rm -rf "$TMP"' EXIT
MY() { docker exec -i wimo-wimo-db sh -c 'exec mysql -u root -p"$MYSQL_ROOT_PASSWORD" -N -B --default-character-set=utf8mb4 wimo'; }
PG() { $PSQL -qAt -v ON_ERROR_STOP=1; }

PASS=0
FAIL=0
BODY=""
STATUS=""
ok() { PASS=$((PASS + 1)); printf '  PASS  %s\n' "$1"; }
ko() { FAIL=$((FAIL + 1)); printf '  FAIL  %s\n' "$1"; [ -n "${2:-}" ] && printf '        %s\n' "$(printf '%s' "$2" | head -c 300)"; }
step() { printf '\n== %s\n' "$1"; }
json() {
  printf '%s' "$BODY" | node -e '
    let s = ""; process.stdin.on("data", d => (s += d)).on("end", () => {
      let j; try { j = JSON.parse(s); } catch { process.stdout.write(""); return; }
      const v = (new Function("j", "return (" + process.argv[1] + ")"))(j);
      process.stdout.write(v === undefined || v === null ? "" : typeof v === "object" ? JSON.stringify(v) : String(v));
    });' "$1"
}
call() {
  local label="$1" expect="$2" method="$3" path="$4" token="$5" data="${6:-}"
  local args=(-s -o "$TMP/body" -w '%{http_code}' -X "$method" -H 'x-client: mobile' -H 'accept: application/json')
  [ "$token" != "-" ] && args+=(-H "authorization: Bearer $token")
  if [ -n "$data" ]; then printf '%s' "$data" >"$TMP/data"; args+=(-H 'content-type: application/json' --data-binary "@$TMP/data"); fi
  STATUS="$(curl "${args[@]}" "$API$path")"
  BODY="$(cat "$TMP/body")"
  # Statut attendu : un code, ou plusieurs séparés par « | » (« 201|409 »).
  if [[ "|$expect|" == *"|$STATUS|"* ]]; then ok "$label ($method → $STATUS)"; return 0; fi
  ko "$label ($method $path → $STATUS, attendu $expect)" "$BODY"
  return 1
}
check() { if [ "$(json "$2")" = "true" ]; then ok "$1"; else ko "$1" "$BODY"; fi; }
same() { if [ "$2" = "$3" ]; then ok "$1 ($2)"; else ko "$1 : $2, attendu $3"; fi; }
login() {
  local token
  call "connexion $1" 200 POST /auth/login - "{\"identifier\":\"$2\",\"password\":\"$3\"}" >/dev/null || return 1
  json 'j.accessToken'
}

DOC_SEQ="$(PG <<<'select coalesce(json_agg(d), $$[]$$) from document_sequences d')" || { echo "PSQL injoignable" >&2; exit 2; }
PARCEL_SEQ="$(PG <<<'select coalesce(json_agg(d), $$[]$$) from parcel_code_sequences d')"

step "Choix des données importées"
# Vendeur : compte actif, des colis importés livrés, refusés ou retournés jamais facturés, aucun brouillon de facture,
# une remise Laravel active si possible (le plus de colis d'abord).
read -r CLIENT_LEGACY CLIENT_ID CLIENT_USER CLIENT_CITY AGENCY <<<"$(PG <<'SQL' | tr '|' ' '
select mc."legacyId", c.id, c."userId", c."cityId", coalesce(c."agencyId", (select id from agencies order by "createdAt" limit 1))
from clients c join legacy_mappings mc on mc."entity" = 'client' and mc."newId" = c.id join users u on u.id = c."userId" and u."isActive" and u."deletedAt" is null
where c."isActive"
  and exists (select 1 from parcels p join legacy_mappings mp on mp."entity" = 'parcel' and mp."newId" = p.id
              where p."clientId" = c.id and p."statusId" in (4, 5, 6) and p."billingState" = 'TO_INVOICE' and not exists (select 1 from invoice_lines l where l."parcelId" = p.id))
  and not exists (select 1 from invoices i where i."clientId" = c.id and i.status = 'DRAFT')
order by exists (select 1 from client_discounts d where d."clientId" = c.id and d."isActive") desc,
  (select count(*) from parcels p where p."clientId" = c.id) desc
limit 1
SQL
)"
[ -n "${CLIENT_ID:-}" ] && ok "vendeur importé : Laravel $CLIENT_LEGACY" || { ko "aucun vendeur importé qui convienne"; exit 1; }
CITY_LEGACY="$(PG <<<"select min(\"legacyId\") from legacy_mappings where \"entity\" = 'city' and \"newId\" = '$CLIENT_CITY'")"
# Destination : une ville tarifée au départ de la ville du vendeur, dans la zone d'une remise active s'il en a une.
read -r DEST_LEGACY EXPECTED_FEE <<<"$(MY <<SQL
SET SESSION TRANSACTION READ ONLY;
SELECT t.destination_id,
  CAST(ROUND(CASE
    WHEN r.id IS NOT NULL AND t.destination_id = t.depart_id AND r.meme_ville THEN r.meme_ville
    WHEN r.id IS NOT NULL AND r.frais_livraison THEN r.frais_livraison
    ELSE COALESCE(t.prix_quartier, 0) END * 100) AS SIGNED)
FROM tarifs t
JOIN villes v ON v.id = t.destination_id AND v.deleted_at IS NULL
LEFT JOIN client_remises r ON r.id = (SELECT MIN(x.id) FROM client_remises x JOIN zones z ON z.id = x.zone_id
  WHERE x.client_id = $CLIENT_LEGACY AND x.enabled = 1 AND FIND_IN_SET(t.destination_id, REPLACE(z.villes, ' ', '')))
WHERE t.depart_id = $CITY_LEGACY AND t.prix_quartier IS NOT NULL
  AND t.id = (SELECT MAX(x.id) FROM tarifs x WHERE x.depart_id = t.depart_id AND x.destination_id = t.destination_id)
ORDER BY r.id IS NULL, t.destination_id = t.depart_id, t.id
LIMIT 1;
SQL
)"
DEST_CITY="$(PG <<<"select \"newId\" from legacy_mappings where \"entity\" = 'city' and \"legacyId\" = '$DEST_LEGACY'")"
[ -n "$DEST_CITY" ] && ok "destination : ville Laravel $DEST_LEGACY, frais attendus $EXPECTED_FEE centimes" || { ko "aucune destination tarifée"; exit 1; }
# Livreur : compte actif, la destination à sa grille, aucune caisse ouverte (la caisse RF- du passage est la sienne).
read -r COURIER_LEGACY COURIER_ID COURIER_USER COURIER_FEE <<<"$(PG <<SQL | tr '|' ' '
select mk."legacyId", k.id, k."userId", (cc."deliveryFee" * 100)::bigint
from couriers k join legacy_mappings mk on mk."entity" = 'courier' and mk."newId" = k.id join users u on u.id = k."userId" and u."isActive" and u."deletedAt" is null
join courier_cities cc on cc."courierId" = k.id and cc."cityId" = '$DEST_CITY' and cc."deliveryFee" is not null
where k."isActive" and not exists (select 1 from cash_remittances r where r."courierId" = k.id and r.status = 'DRAFT')
order by (select count(*) from routes r where r."courierId" = k.id) desc limit 1
SQL
)"
[ -n "${COURIER_ID:-}" ] && ok "livreur importé : Laravel $COURIER_LEGACY (frais grille $COURIER_FEE centimes)" || { ko "aucun livreur importé qui convienne"; exit 1; }
# Empreintes d'origine, remises à la fin (jamais affichées).
PG <<SQL >"$TMP/hashes"
select id || '|' || "passwordHash" || '|' || "mustChangePassword" || '|' || to_char("tokensValidFrom" at time zone 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
from users where id in ('$CLIENT_USER', '$COURIER_USER')
SQL
[ "$(wc -l <"$TMP/hashes")" = 2 ] && ok "empreintes d'origine des deux comptes gardées" || { ko "empreintes d'origine illisibles"; exit 1; }

ADMIN="$(login admin "$ADMIN_EMAIL" "$ADMIN_PASSWORD")" || { ko "admin connecté"; exit 1; }
TEMP_PASSWORD="Reprise-$RUN!"

step "1. Connexion d'un vendeur et d'un livreur importés"
CLIENT_LOGIN="$(PG <<<"select coalesce(email, username) from users where id = '$CLIENT_USER'")"
COURIER_LOGIN="$(PG <<<"select coalesce(email, username) from users where id = '$COURIER_USER'")"
call "mot de passe de test du vendeur" 201 POST "/users/$CLIENT_USER/set-password" "$ADMIN" "{\"password\":\"$TEMP_PASSWORD\",\"mustChangePassword\":false}"
call "mot de passe de test du livreur" 201 POST "/users/$COURIER_USER/set-password" "$ADMIN" "{\"password\":\"$TEMP_PASSWORD\",\"mustChangePassword\":false}"
SELLER="$(login "vendeur importé" "$CLIENT_LOGIN" "$TEMP_PASSWORD")"
PHONE="$(login "livreur importé" "$COURIER_LOGIN" "$TEMP_PASSWORD")"
call "profil du vendeur" 200 GET /auth/me "$SELLER" && check "rôle vendeur" 'JSON.stringify(j).includes("MERCHANT")'
call "profil du livreur" 200 GET /courier/me "$PHONE"

step "2. Le vendeur voit ses colis et ses factures historiques"
EXPECTED_PARCELS="$(MY <<<"SELECT COUNT(*) FROM colis WHERE deleted_at IS NULL AND client_id = $CLIENT_LEGACY")"
call "mes colis" 200 GET "/parcels?pageSize=1" "$SELLER" && same "nombre de colis = Laravel" "$(json 'j.total')" "$EXPECTED_PARCELS"
read -r EXPECTED_INVOICES EXPECTED_NET <<<"$(MY <<SQL
SET SESSION TRANSACTION READ ONLY;
SELECT COUNT(DISTINCT f.id), CAST(ROUND(SUM(CASE WHEN c.id IS NULL THEN 0 ELSE
  (CASE WHEN c.statut_id = 4 AND c.frais_echoue IS NULL THEN
      (CASE WHEN c.deja_paye != 1 THEN CAST(COALESCE(NULLIF(c.prix, ''), '0') AS DECIMAL(14,2)) ELSE 0 END)
      + (CASE WHEN c.frais_inclus != 1 THEN COALESCE(c.frais, 0) ELSE 0 END) - COALESCE(c.frais, 0) ELSE 0 END)
  - COALESCE(c.frais_echoue, 0) + (CASE WHEN c.statut_id = 4 AND c.frais_deja_paye = 1 THEN COALESCE(c.frais, 0) ELSE 0 END) END) * 100) AS SIGNED)
FROM factures f LEFT JOIN facture_colis fc ON fc.facture_id = f.id LEFT JOIN colis c ON c.id = fc.colis_id AND c.deleted_at IS NULL
WHERE f.client_id = $CLIENT_LEGACY;
SQL
)"
call "mes factures" 200 GET "/invoices?pageSize=500" "$SELLER"
same "nombre de factures = Laravel" "$(json 'j.total')" "$EXPECTED_INVOICES"
same "net de toutes ses factures = Laravel (centimes)" "$(json 'j.items.reduce((s, i) => s + i.totalDue, 0)')" "${EXPECTED_NET:-0}"
IMPORTED_INVOICE="$(json '(j.items.find(i => i.status === "paid") || j.items.find(i => i.status === "issued") || {}).code')"

step "3. Nouveau colis du vendeur : tarif de sa ville avec sa remise"
call "création d'un colis" 201 POST /parcels "$SELLER" \
  "{\"clientId\":\"$CLIENT_ID\",\"description\":\"Recette reprise $RUN\",\"recipientName\":\"Recette\",\"recipientPhone\":\"0611111111\",\"originCityId\":\"$CLIENT_CITY\",\"pickupAddress\":\"Dépôt\",\"destinationCityId\":\"$DEST_CITY\",\"deliveryAddress\":\"Rue 1\",\"codAmount\":250}"
PARCEL_ID="$(json 'j.id')"
PARCEL="$(json 'j.code')"
same "frais du nouveau colis = règle Laravel (centimes)" "$(json 'j.deliveryFee')" "$EXPECTED_FEE"

step "4. Circuit avec le livreur importé, livraison, caisse RF-, facture sur des colis importés"
call "demande de ramassage" 200 POST /merchant/me/pickups "$SELLER" "{\"parcelIds\":[\"$PARCEL_ID\"]}"
PICKUP="$(json 'j.code')"
call "validation du bon $PICKUP" 201 POST "/delivery-notes/$PICKUP/validate" "$ADMIN" "{\"parcelIds\":[\"$PARCEL_ID\"]}"
call "circuit" 201 POST /routes "$ADMIN" "{\"cityId\":\"$DEST_CITY\",\"courierId\":\"$COURIER_ID\"}"
ROUTE="$(json 'j.code')"
call "colis au circuit" 201 POST "/routes/$ROUTE/parcels" "$ADMIN" "{\"parcelIds\":[\"$PARCEL_ID\"]}"
call "départ du circuit" 201 POST "/routes/$ROUTE/dispatch" "$ADMIN"
call "tournées du livreur importé" 200 GET /courier/me/routes "$PHONE" && check "le circuit est affecté" "JSON.stringify(j).includes('$ROUTE')"
call "livraison" 200 POST "/courier/parcels/$PARCEL/status" "$PHONE" '{"statusId":4}'
call "caisse du livreur" 200 GET /courier/me/remittance "$PHONE"
REMITTANCE="$(json 'j.remittance && j.remittance.code')"
check "caisse RF- ouverte" "(j.remittance && j.remittance.code || '').startsWith('RF-')"
same "COD attendu (centimes)" "$(json 'j.remittance && j.remittance.expectedCod')" 25000
same "frais du livreur = sa grille importée (centimes)" "$(json 'j.remittance && j.remittance.expectedCommission')" "$COURIER_FEE"
# Facture brouillon .NET sur 5 colis importés non facturés de ce vendeur : montants = formule Laravel.
PG <<SQL >"$TMP/billable"
select mp."legacyId" || '|' || p.id from parcels p join legacy_mappings mp on mp."entity" = 'parcel' and mp."newId" = p.id
where p."clientId" = '$CLIENT_ID' and p."statusId" in (4, 5, 6) and p."billingState" = 'TO_INVOICE' and not exists (select 1 from invoice_lines l where l."parcelId" = p.id)
order by p."createdAt" limit 5
SQL
LEGACY_IDS="$(cut -d'|' -f1 "$TMP/billable" | paste -sd, -)"
PARCEL_IDS="$(cut -d'|' -f2 "$TMP/billable" | sed 's/.*/"&"/' | paste -sd, -)"
EXPECTED_DRAFT="$(MY <<SQL
SET SESSION TRANSACTION READ ONLY;
SELECT CAST(ROUND(SUM(
  (CASE WHEN c.statut_id = 4 AND c.frais_echoue IS NULL THEN
      (CASE WHEN c.deja_paye != 1 THEN CAST(COALESCE(NULLIF(c.prix, ''), '0') AS DECIMAL(14,2)) ELSE 0 END)
      + (CASE WHEN c.frais_inclus != 1 THEN COALESCE(c.frais, 0) ELSE 0 END) - COALESCE(c.frais, 0) ELSE 0 END)
  - COALESCE(c.frais_echoue, 0)) * 100) AS SIGNED)
FROM colis c WHERE c.id IN ($LEGACY_IDS);
SQL
)"
# Une livraison peut avoir ouvert le brouillon du vendeur (409 + son code) : il sert alors, ses autres lignes ne comptent pas.
call "facture brouillon" "201|409" POST /invoices "$ADMIN" "{\"clientId\":\"$CLIENT_ID\",\"agencyId\":\"$AGENCY\"}"
INVOICE="$(json 'j.code || (j.details && j.details.invoiceCode)')"
call "colis importés non facturés ajoutés" 201 POST "/invoices/$INVOICE/parcels" "$ADMIN" "{\"parcelIds\":[$PARCEL_IDS]}"
call "brouillon $INVOICE" 200 GET "/invoices/$INVOICE" "$ADMIN"
IDS_JS="[$PARCEL_IDS]"
same "net des colis importés du brouillon = formule Laravel (centimes)" "$(json "j.lines.filter(l => $IDS_JS.includes(l.parcelId)).reduce((s, l) => s + l.netDue, 0)")" "$EXPECTED_DRAFT"
same "colis importés au brouillon" "$(json "j.lines.filter(l => $IDS_JS.includes(l.parcelId)).length")" "$(wc -l <"$TMP/billable" | tr -d ' ')"
call "brouillon supprimé" 200 DELETE "/invoices/$INVOICE" "$ADMIN"

step "5. Les documents importés ne se modifient pas"
if [ -n "$IMPORTED_INVOICE" ]; then
  call "colis ajouté à la facture importée $IMPORTED_INVOICE" 409 POST "/invoices/$IMPORTED_INVOICE/parcels" "$ADMIN" "{\"parcelIds\":[\"$PARCEL_ID\"]}"
  call "suppression de la facture importée" 409 DELETE "/invoices/$IMPORTED_INVOICE" "$ADMIN"
  call "émission de la facture importée" 409 POST "/invoices/$IMPORTED_INVOICE/issue" "$ADMIN" '{}'
else
  ko "le vendeur n'a aucune facture importée validée ou payée"
fi
IMPORTED_PAYMENT="$(PG <<<"select p.id from payments p join legacy_mappings m on m.\"entity\" = 'payment' and m.\"newId\" = p.id order by p.\"paidAt\" desc limit 1")"
call "suppression d'un règlement importé" 409 DELETE "/payments/$IMPORTED_PAYMENT" "$ADMIN" && check "motif : document importé" 'j.details && j.details.reason === "imported-document-locked"'
IMPORTED_REMITTANCE="$(PG <<<"select r.code from cash_remittances r join legacy_mappings m on m.\"entity\" = 'cash_remittance' and m.\"newId\" = r.id where r.status = 'CONFIRMED' limit 1")"
call "suppression d'une caisse importée" 409 DELETE "/remittances/$IMPORTED_REMITTANCE" "$ADMIN"

step "Nettoyage : ce que le passage a créé est supprimé, empreintes d'origine remises"
out="$($PSQL -qAt -F' | ' -v parcel="${PARCEL_ID:-}" -v client_user="$CLIENT_USER" -v courier_user="$COURIER_USER" -v since="$SINCE" \
  -v hashes="$(sed 's/.*/"&"/' "$TMP/hashes" | paste -sd, - | sed 's/^/[/; s/$/]/')" -v doc_seq="$DOC_SEQ" -v parcel_seq="$PARCEL_SEQ" \
  <"$HERE/recette-reprise-cleanup.sql" 2>&1)" && ok "suppression en base ($(printf '%s' "$out" | tail -1))" || ko "suppression en base" "$out"
[ -n "${PARCEL:-}" ] && call "le colis de recette n'existe plus" 404 GET "/parcels/$PARCEL" "$ADMIN"
[ -n "${ROUTE:-}" ] && call "le circuit de recette n'existe plus" 404 GET "/routes/$ROUTE" "$ADMIN"
[ -n "${REMITTANCE:-}" ] && call "la caisse de recette n'existe plus" 404 GET "/remittances/$REMITTANCE" "$ADMIN"
code="$(curl -s -o /dev/null -w '%{http_code}' -H 'x-client: mobile' -H 'content-type: application/json' \
  --data "{\"identifier\":\"$CLIENT_LOGIN\",\"password\":\"$TEMP_PASSWORD\"}" "$API/auth/login")"
[ "$code" != 200 ] && ok "le mot de passe de test ne sert plus ($code)" || ko "le mot de passe de test sert encore"
same "empreintes d'origine remises" "$(PG <<<"select count(*) from users u where (u.id || '|' || u.\"passwordHash\") in ($(cut -d'|' -f1,2 "$TMP/hashes" | sed "s/.*/'&'/" | paste -sd, -))")" 2

printf '\n%s PASS, %s FAIL\n' "$PASS" "$FAIL"
if [ "$FAIL" -eq 0 ]; then echo "RECETTE REPRISE PASS"; exit 0; fi
echo "RECETTE REPRISE FAIL"
exit 1
