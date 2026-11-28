#!/bin/sh
# Contrôles par échantillon de la reprise Wimo Laravel, champ par champ (source MariaDB en lecture seule, cible .NET).
#   sh samples.sh <base-postgres> <dossier-sortie>
# Valeurs comparées après suppression des retours à la ligne et des blancs de bord (l'import ôte ces derniers).
# Échantillon : 10 colis (2 échanges ECH-, 2 retours RET-, livrés, échoués, en cours), 5 clients, 5 livreurs, 3 factures,
# 3 circuits. Affiche par enregistrement le nombre de champs identiques et le NOM des champs qui diffèrent, jamais les
# valeurs (données personnelles) ; les relevés complets restent dans <dossier-sortie>. Code 1 si un champ diffère.
set -eu
DB=${1:?base PostgreSQL}
OUT=${2:?dossier de sortie}
MY() { docker exec -i wimo-wimo-db sh -c 'exec mysql -u root -p"$MYSQL_ROOT_PASSWORD" -N -B --default-character-set=utf8mb4 wimo'; }
PG() { docker exec -i wimo-delivery-dotnet-postgres-1 psql -q -U wimo -d "$DB" -v ON_ERROR_STOP=1 -At; }

IDS=$(MY <<'SQL'
SET SESSION TRANSACTION READ ONLY;
SELECT CONCAT('colis=', GROUP_CONCAT(id)) FROM (
  (SELECT id FROM colis WHERE deleted_at IS NULL AND code LIKE 'ECH-%' AND reference_id IN (SELECT id FROM colis WHERE deleted_at IS NULL) ORDER BY id LIMIT 2)
  UNION (SELECT id FROM colis WHERE deleted_at IS NULL AND code LIKE 'RET-%' ORDER BY id LIMIT 2)
  UNION (SELECT id FROM colis WHERE deleted_at IS NULL AND statut_id = 4 AND frais_inclus = 0 ORDER BY id DESC LIMIT 1)
  UNION (SELECT id FROM colis WHERE deleted_at IS NULL AND statut_id = 4 ORDER BY id LIMIT 1)
  UNION (SELECT id FROM colis WHERE deleted_at IS NULL AND frais_echoue IS NOT NULL ORDER BY id DESC LIMIT 1)
  UNION (SELECT id FROM colis WHERE deleted_at IS NULL AND statut_id = 36 ORDER BY id LIMIT 1)
  UNION (SELECT id FROM colis WHERE deleted_at IS NULL AND statut_id IN (13, 14, 16) ORDER BY id DESC LIMIT 1)
  UNION (SELECT id FROM colis WHERE deleted_at IS NULL AND statut_id = 23 ORDER BY id DESC LIMIT 1)
) x;
SELECT CONCAT('clients=', GROUP_CONCAT(id)) FROM (SELECT c.id FROM clients c JOIN colis k ON k.client_id = c.id AND k.deleted_at IS NULL
  WHERE c.deleted_at IS NULL GROUP BY c.id ORDER BY COUNT(*) DESC LIMIT 5) x;
SELECT CONCAT('livreurs=', GROUP_CONCAT(id)) FROM (SELECT l.id FROM livreurs l JOIN recettes r ON r.partenaire_id = l.id AND r.partenaire_type = 'App\\Livreur'
  WHERE l.deleted_at IS NULL GROUP BY l.id ORDER BY COUNT(*) DESC LIMIT 5) x;
SELECT CONCAT('factures=', GROUP_CONCAT(id)) FROM (
  (SELECT id FROM factures WHERE client_id IS NOT NULL AND statut_id = 3 ORDER BY id DESC LIMIT 1)
  UNION (SELECT id FROM factures WHERE client_id IS NOT NULL AND statut_id = 2 ORDER BY id DESC LIMIT 1)
  UNION (SELECT id FROM factures WHERE client_id IS NOT NULL AND statut_id = 1 ORDER BY id DESC LIMIT 1)
) x;
SELECT CONCAT('circuits=', GROUP_CONCAT(id)) FROM (
  (SELECT b.id FROM boxes b JOIN boxe_colis bc ON bc.boxe_id = b.id GROUP BY b.id ORDER BY COUNT(*) DESC LIMIT 1)
  UNION (SELECT id FROM boxes ORDER BY id DESC LIMIT 1)
  UNION (SELECT id FROM boxes WHERE statut_id = 9 LIMIT 1)
) x;
SQL
)
v() { echo "$IDS" | sed -n "s/^$1=//p"; }
COLIS=$(v colis); CLIENTS=$(v clients); LIVREURS=$(v livreurs); FACTURES=$(v factures); CIRCUITS=$(v circuits)

MY <<SQL | sort > "$OUT/echantillon-source.txt"
SET SESSION TRANSACTION READ ONLY;
SELECT CONCAT('colis ', c.id, '|', f, '|', COALESCE(TRIM(REPLACE(REPLACE(REPLACE(val, CHAR(13), ' '), CHAR(10), ' '), CHAR(9), ' ')), '∅')) FROM colis c JOIN (
  SELECT id, 'code' f, code val FROM colis
  UNION ALL SELECT id, 'statut', CASE statut_id WHEN 15 THEN 16 WHEN 30 THEN 7 WHEN 36 THEN 33 ELSE statut_id END FROM colis
  UNION ALL SELECT id, 'cod', FORMAT(CAST(COALESCE(NULLIF(prix, ''), '0') AS DECIMAL(14,2)), 2, 'en_US') FROM colis
  UNION ALL SELECT id, 'frais', FORMAT(COALESCE(frais, 0), 2, 'en_US') FROM colis
  UNION ALL SELECT id, 'frais_echoue', FORMAT(frais_echoue, 2, 'en_US') FROM colis
  UNION ALL SELECT id, 'frais_inclus', IF(frais_inclus = 1, 't', 'f') FROM colis
  UNION ALL SELECT id, 'deja_paye', IF(deja_paye = 1, 't', 'f') FROM colis
  UNION ALL SELECT id, 'destinataire', destinataire FROM colis
  UNION ALL SELECT id, 'telephone', tel_destinataire FROM colis
  UNION ALL SELECT id, 'adresse', livraison_adresse FROM colis
  UNION ALL SELECT id, 'client', client_id FROM colis
  UNION ALL SELECT id, 'livreur', livreur_id FROM colis
  UNION ALL SELECT id, 'ville_depart', ramassage_ville_id FROM colis
  UNION ALL SELECT id, 'ville_livraison', livraison_ville_id FROM colis
  UNION ALL SELECT id, 'reference', (SELECT r.code FROM colis r WHERE r.id = colis.reference_id) FROM colis
  UNION ALL SELECT id, 'cree_le', DATE_FORMAT(created_at, '%Y-%m-%d %H:%i:%s') FROM colis
  UNION ALL SELECT colis_id, 'historique', COUNT(*) FROM colis_trakings GROUP BY colis_id
  UNION ALL SELECT colis_id, 'dernier_statut_historique', SUBSTRING_INDEX(GROUP_CONCAT(CASE statut_id WHEN 15 THEN 16 WHEN 30 THEN 7 WHEN 36 THEN 33 ELSE statut_id END ORDER BY created_at DESC, id DESC), ',', 1) FROM colis_trakings WHERE statut_id IS NOT NULL GROUP BY colis_id
) x ON x.id = c.id WHERE c.id IN ($COLIS);
SELECT CONCAT('client ', k.id, '|', f, '|', COALESCE(TRIM(REPLACE(REPLACE(REPLACE(val, CHAR(13), ' '), CHAR(10), ' '), CHAR(9), ' ')), '∅')) FROM clients k JOIN (
  SELECT c.id, 'nom' f, COALESCE(c.label, TRIM(CONCAT(COALESCE(u.nom, ''), ' ', COALESCE(u.prenom, '')))) val FROM clients c JOIN users u ON u.id = c.user_id
  UNION ALL SELECT c.id, 'email', LOWER(u.email) FROM clients c JOIN users u ON u.id = c.user_id
  UNION ALL SELECT c.id, 'telephone', u.tel FROM clients c JOIN users u ON u.id = c.user_id
  UNION ALL SELECT c.id, 'actif', IF(c.enabled = 1, 't', 'f') FROM clients c
  UNION ALL SELECT c.id, 'type', IF(c.type = 'particulier', 'INDIVIDUAL', 'MERCHANT') FROM clients c
  UNION ALL SELECT c.id, 'mot_de_passe', u.password FROM clients c JOIN users u ON u.id = c.user_id
  UNION ALL SELECT client_id, 'colis', COUNT(*) FROM colis WHERE deleted_at IS NULL GROUP BY client_id
  UNION ALL SELECT client_id, 'factures', COUNT(*) FROM factures WHERE client_id IS NOT NULL GROUP BY client_id
) x ON x.id = k.id WHERE k.id IN ($CLIENTS);
SELECT CONCAT('livreur ', l.id, '|', f, '|', COALESCE(TRIM(REPLACE(REPLACE(REPLACE(val, CHAR(13), ' '), CHAR(10), ' '), CHAR(9), ' ')), '∅')) FROM livreurs l JOIN (
  SELECT l.id, 'email' f, LOWER(u.email) val FROM livreurs l JOIN users u ON u.id = l.user_id
  UNION ALL SELECT l.id, 'telephone', u.tel FROM livreurs l JOIN users u ON u.id = l.user_id
  UNION ALL SELECT l.id, 'ville', l.ville_id FROM livreurs l
  UNION ALL SELECT l.id, 'mot_de_passe', u.password FROM livreurs l JOIN users u ON u.id = l.user_id
  UNION ALL SELECT livreur_id, 'villes_tarifees', COUNT(DISTINCT ville_id) FROM livreur_ville WHERE frais_livraison IS NOT NULL GROUP BY livreur_id
  UNION ALL SELECT partenaire_id, 'recettes', COUNT(*) FROM recettes WHERE partenaire_type = 'App\\\\Livreur' GROUP BY partenaire_id
  UNION ALL SELECT destinataire_id, 'circuits', COUNT(*) FROM boxes WHERE destinataire_type = 'App\\\\Livreur' GROUP BY destinataire_id
) x ON x.id = l.id WHERE l.id IN ($LIVREURS);
SELECT CONCAT('facture ', f.id, '|', k, '|', COALESCE(TRIM(REPLACE(REPLACE(REPLACE(val, CHAR(13), ' '), CHAR(10), ' '), CHAR(9), ' ')), '∅')) FROM factures f JOIN (
  SELECT id, 'code' k, code val FROM factures
  UNION ALL SELECT id, 'statut', ELT(statut_id, 'DRAFT', 'ISSUED', 'PAID') FROM factures
  UNION ALL SELECT id, 'client', client_id FROM factures
  UNION ALL SELECT fc.facture_id, 'colis', GROUP_CONCAT(c.code ORDER BY c.code) FROM facture_colis fc JOIN colis c ON c.id = fc.colis_id AND c.deleted_at IS NULL GROUP BY fc.facture_id
  UNION ALL SELECT facture_id, 'reglements', FORMAT(SUM(montant), 2, 'en_US') FROM rembourssements GROUP BY facture_id
) x ON x.id = f.id WHERE f.id IN ($FACTURES);
SELECT CONCAT('circuit ', b.id, '|', k, '|', COALESCE(TRIM(REPLACE(REPLACE(REPLACE(val, CHAR(13), ' '), CHAR(10), ' '), CHAR(9), ' ')), '∅')) FROM boxes b JOIN (
  SELECT id, 'code' k, code val FROM boxes
  UNION ALL SELECT id, 'livreur', destinataire_id FROM boxes
  UNION ALL SELECT id, 'ville', ville_id FROM boxes
  UNION ALL SELECT id, 'sortie', DATE_FORMAT(date_sorti, '%Y-%m-%d') FROM boxes
  UNION ALL SELECT bc.boxe_id, 'colis', GROUP_CONCAT(c.code ORDER BY c.code) FROM boxe_colis bc JOIN colis c ON c.id = bc.colis_id AND c.deleted_at IS NULL GROUP BY bc.boxe_id
) x ON x.id = b.id WHERE b.id IN ($CIRCUITS);
SQL

PG <<SQL | sort > "$OUT/echantillon-cible.txt"
CREATE TEMP VIEW m AS SELECT "entity", "legacyId", "newId" FROM legacy_mappings;
CREATE TEMP VIEW pp AS SELECT mp."legacyId" lid, p.* FROM parcels p JOIN m mp ON mp."entity" = 'parcel' AND mp."newId" = p.id WHERE mp."legacyId" IN ($(echo "$COLIS" | sed "s/[0-9][0-9]*/'&'/g"));
SELECT 'colis ' || lid || '|' || f || '|' || COALESCE(btrim(regexp_replace(val, '[[:cntrl:]]', ' ', 'g')), '∅') FROM (
  SELECT lid, 'code' f, code val FROM pp
  UNION ALL SELECT lid, 'statut', "statusId"::text FROM pp
  UNION ALL SELECT lid, 'cod', to_char("codAmount", 'FM999,999,990.00') FROM pp
  UNION ALL SELECT lid, 'frais', to_char("deliveryFee", 'FM999,999,990.00') FROM pp
  UNION ALL SELECT lid, 'frais_echoue', to_char("failureFee", 'FM999,999,990.00') FROM pp
  UNION ALL SELECT lid, 'frais_inclus', CASE WHEN "feeIncluded" THEN 't' ELSE 'f' END FROM pp
  UNION ALL SELECT lid, 'deja_paye', CASE WHEN "alreadyPaid" THEN 't' ELSE 'f' END FROM pp
  UNION ALL SELECT lid, 'destinataire', NULLIF("recipientName", '') FROM pp
  UNION ALL SELECT lid, 'telephone', NULLIF("recipientPhone", '') FROM pp
  UNION ALL SELECT lid, 'adresse', NULLIF("deliveryAddress", '') FROM pp
  UNION ALL SELECT lid, 'client', (SELECT "legacyId" FROM m WHERE "entity" = 'client' AND "newId" = pp."clientId") FROM pp
  UNION ALL SELECT lid, 'livreur', (SELECT "legacyId" FROM m WHERE "entity" = 'courier' AND "newId" = pp."courierId") FROM pp
  UNION ALL SELECT lid, 'ville_depart', (SELECT min("legacyId") FROM m WHERE "entity" = 'city' AND "newId" = pp."originCityId") FROM pp
  UNION ALL SELECT lid, 'ville_livraison', (SELECT min("legacyId") FROM m WHERE "entity" = 'city' AND "newId" = pp."destinationCityId") FROM pp
  UNION ALL SELECT lid, 'reference', (SELECT r.code FROM parcels r WHERE r.id = pp."referenceParcelId") FROM pp
  UNION ALL SELECT lid, 'cree_le', to_char("createdAt" AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS') FROM pp
  UNION ALL SELECT lid, 'historique', (SELECT count(*)::text FROM parcel_events e WHERE e."parcelId" = pp.id) FROM pp
  UNION ALL SELECT lid, 'dernier_statut_historique', (SELECT e."statusId"::text FROM parcel_events e JOIN m me ON me."entity" = 'parcel_event' AND me."newId" = e.id
      WHERE e."parcelId" = pp.id ORDER BY e."createdAt" DESC, me."legacyId"::int DESC LIMIT 1) FROM pp
) x WHERE val IS NOT NULL OR f IN ('frais_echoue', 'livreur', 'reference');
CREATE TEMP VIEW cc AS SELECT mc."legacyId" lid, c.* FROM clients c JOIN m mc ON mc."entity" = 'client' AND mc."newId" = c.id WHERE mc."legacyId" IN ($(echo "$CLIENTS" | sed "s/[0-9][0-9]*/'&'/g"));
SELECT 'client ' || lid || '|' || f || '|' || COALESCE(btrim(regexp_replace(val, '[[:cntrl:]]', ' ', 'g')), '∅') FROM (
  SELECT lid, 'nom' f, name val FROM cc
  UNION ALL SELECT lid, 'email', lower(u.email) FROM cc JOIN users u ON u.id = cc."userId"
  UNION ALL SELECT lid, 'telephone', u.phone FROM cc JOIN users u ON u.id = cc."userId"
  UNION ALL SELECT lid, 'actif', CASE WHEN "isActive" THEN 't' ELSE 'f' END FROM cc
  UNION ALL SELECT lid, 'type', type::text FROM cc
  UNION ALL SELECT lid, 'mot_de_passe', u."passwordHash" FROM cc JOIN users u ON u.id = cc."userId"
  UNION ALL SELECT lid, 'colis', (SELECT count(*)::text FROM parcels p JOIN m mp ON mp."entity" = 'parcel' AND mp."newId" = p.id WHERE p."clientId" = cc.id) FROM cc
  UNION ALL SELECT lid, 'factures', (SELECT count(*)::text FROM invoices i JOIN m mi ON mi."entity" = 'invoice' AND mi."newId" = i.id WHERE i."clientId" = cc.id) FROM cc
) x;
CREATE TEMP VIEW kk AS SELECT mk."legacyId" lid, k.* FROM couriers k JOIN m mk ON mk."entity" = 'courier' AND mk."newId" = k.id WHERE mk."legacyId" IN ($(echo "$LIVREURS" | sed "s/[0-9][0-9]*/'&'/g"));
SELECT 'livreur ' || lid || '|' || f || '|' || COALESCE(btrim(regexp_replace(val, '[[:cntrl:]]', ' ', 'g')), '∅') FROM (
  SELECT lid, 'email' f, lower(u.email) val FROM kk JOIN users u ON u.id = kk."userId"
  UNION ALL SELECT lid, 'telephone', kk.phone FROM kk
  UNION ALL SELECT lid, 'ville', (SELECT min("legacyId") FROM m WHERE "entity" = 'city' AND "newId" = kk."cityId") FROM kk
  UNION ALL SELECT lid, 'mot_de_passe', u."passwordHash" FROM kk JOIN users u ON u.id = kk."userId"
  UNION ALL SELECT lid, 'villes_tarifees', (SELECT count(*)::text FROM courier_cities x WHERE x."courierId" = kk.id AND x."deliveryFee" IS NOT NULL) FROM kk
  UNION ALL SELECT lid, 'recettes', (SELECT count(*)::text FROM cash_remittances r JOIN m mr ON mr."entity" = 'cash_remittance' AND mr."newId" = r.id WHERE r."courierId" = kk.id) FROM kk
  UNION ALL SELECT lid, 'circuits', (SELECT count(*)::text FROM routes r JOIN m mr ON mr."entity" = 'route' AND mr."newId" = r.id WHERE r."courierId" = kk.id) FROM kk
) x;
CREATE TEMP VIEW ii AS SELECT mi."legacyId" lid, i.* FROM invoices i JOIN m mi ON mi."entity" = 'invoice' AND mi."newId" = i.id WHERE mi."legacyId" IN ($(echo "$FACTURES" | sed "s/[0-9][0-9]*/'&'/g"));
SELECT 'facture ' || lid || '|' || k || '|' || COALESCE(btrim(regexp_replace(val, '[[:cntrl:]]', ' ', 'g')), '∅') FROM (
  SELECT lid, 'code' k, code val FROM ii
  UNION ALL SELECT lid, 'statut', status::text FROM ii
  UNION ALL SELECT lid, 'client', (SELECT "legacyId" FROM m WHERE "entity" = 'client' AND "newId" = ii."clientId") FROM ii
  UNION ALL SELECT lid, 'colis', (SELECT string_agg(l."parcelCode", ',' ORDER BY l."parcelCode" COLLATE "C") FROM invoice_lines l WHERE l."invoiceId" = ii.id) FROM ii
  UNION ALL SELECT lid, 'reglements', (SELECT to_char(sum(p.amount), 'FM999,999,990.00') FROM payments p WHERE p."invoiceId" = ii.id) FROM ii
) x WHERE val IS NOT NULL;
CREATE TEMP VIEW rr AS SELECT mr."legacyId" lid, r.* FROM routes r JOIN m mr ON mr."entity" = 'route' AND mr."newId" = r.id WHERE mr."legacyId" IN ($(echo "$CIRCUITS" | sed "s/[0-9][0-9]*/'&'/g"));
SELECT 'circuit ' || lid || '|' || k || '|' || COALESCE(btrim(regexp_replace(val, '[[:cntrl:]]', ' ', 'g')), '∅') FROM (
  SELECT lid, 'code' k, code val FROM rr
  UNION ALL SELECT lid, 'livreur', (SELECT "legacyId" FROM m WHERE "entity" = 'courier' AND "newId" = rr."courierId") FROM rr
  UNION ALL SELECT lid, 'ville', (SELECT min("legacyId") FROM m WHERE "entity" = 'city' AND "newId" = rr."cityId") FROM rr
  UNION ALL SELECT lid, 'sortie', to_char("exitDate" AT TIME ZONE 'Africa/Casablanca', 'YYYY-MM-DD') FROM rr
  UNION ALL SELECT lid, 'colis', (SELECT string_agg(p.code, ',' ORDER BY p.code COLLATE "C") FROM route_parcels l JOIN parcels p ON p.id = l."parcelId" WHERE l."routeId" = rr.id) FROM rr
) x WHERE val IS NOT NULL;
SQL

# Lignes source « ∅ » sans équivalent cible (valeur absente des deux côtés) : retirées des deux relevés.
grep -v '|∅$' "$OUT/echantillon-source.txt" > "$OUT/s.tmp" || true
grep -v '|∅$' "$OUT/echantillon-cible.txt" > "$OUT/c.tmp" || true
STATUS=0
for record in $(cut -d'|' -f1 "$OUT/s.tmp" | sed 's/ /_/' | sort -u); do
  key=$(echo "$record" | sed 's/_/ /')
  total=$(grep -c "^$key|" "$OUT/s.tmp" || true)
  bad=$(grep "^$key|" "$OUT/s.tmp" | while IFS= read -r line; do grep -qxF "$line" "$OUT/c.tmp" || echo "$line" | cut -d'|' -f2; done | tr '\n' ' ')
  if [ -z "$bad" ]; then echo "OK  $key : $total/$total champs identiques"; else echo "KO  $key : champs différents : $bad"; STATUS=1; fi
done
rm -f "$OUT/s.tmp" "$OUT/c.tmp"
exit $STATUS
