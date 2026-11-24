#!/usr/bin/env bash
# Export, depuis la base de la pile NestJS Wimo-Delivery, des SEULES données reprises par la pile .NET :
# référentiel (zones, villes, quartiers, agences, tarifs trois modes, tarifs de zone), statuts et transitions
# (pour comparaison), paramètres société, et le compte wimo@gmail.com (empreinte du mot de passe conservée).
# Aucune donnée transactionnelle. Lecture seule. Lancé sur le VPS :
#
#   ssh wimo-vps 'bash -s' < deploy/export-referential.sh
set -euo pipefail
cd /opt/wimo-delivery
set -a; . ./.env; set +a
OUT=/opt/wimo-delivery/.runtime/export-referentiel
mkdir -p "$OUT"
chmod 700 "$OUT"

q() { docker exec wimo-delivery-postgres-1 psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -qAt -c "COPY ($2) TO STDOUT WITH (FORMAT csv, HEADER true)" >"$OUT/$1"; }

q zones.csv 'select name, "baseRate", "sameCityRate", "deliveryDays", "isActive" from zones where "deletedAt" is null order by name'
q cities.csv 'select c.code, c.name, c."nameAr", z.name as zone, c."deliveryDelay", c."isActive" from cities c join zones z on z.id = c."zoneId" where c."deletedAt" is null order by c.code'
q districts.csv 'select c.code as "cityCode", d.name, d."isActive" from districts d join cities c on c.id = d."cityId" where d."deletedAt" is null and c."deletedAt" is null order by c.code, d.name'
q agencies.csv 'select a.name, c.code as "cityCode", a.phone, a.email, a.address, (select string_agg(cc.code, $$;$$ order by cc.code) from agency_cities ac join cities cc on cc.id = ac."cityId" where ac."agencyId" = a.id) as "coveredCityCodes", a."isActive" from agencies a join cities c on c.id = a."cityId" where a."deletedAt" is null order by a.name'
q pricing-rules.csv 'select o.code as "originCode", d.code as "destinationCode", r."rateDistrict", r."rateAgency", r."rateRelayPoint", r."isEnabled" from pricing_rules r join cities o on o.id = r."originCityId" join cities d on d.id = r."destinationCityId" where r."deletedAt" is null order by 1, 2'
q zone-rates.csv 'select f.name as "fromZone", t.name as "toZone", r.rate, r."deliveryDays", r."isActive" from zone_rates r join zones f on f.id = r."fromZoneId" join zones t on t.id = r."toZoneId" order by 1, 2'
q parcel-statuses.csv 'select id, key, "group", color, icon, "sortOrder" from parcel_statuses order by id'
q parcel-transitions.csv 'select * from parcel_transitions order by 1, 2'
q company.csv 'select name, "legalName", "taxId", rc, address, city, phone, email, website, "logoUrl", "primaryColor", "secondaryColor", "logoBackground", timezone, "defaultLocale" from companies'
q settings.csv 'select key, value::text as value from settings where scope = $$COMPANY$$ order by key'
q wimo-account.csv 'select u.email, u."firstName", u."lastName", u.phone, u."passwordHash", u.status, (select string_agg(r.role::text, $$;$$) from user_roles r where r."userId" = u.id) as roles from users u where lower(u.email) = $$wimo@gmail.com$$'
chmod 600 "$OUT"/*.csv

echo "Export : $OUT"
for f in "$OUT"/*.csv; do printf '  %-24s %6s lignes\n' "$(basename "$f")" "$(($(wc -l <"$f") - 1))"; done
docker exec wimo-delivery-postgres-1 psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -qAt -F' | ' -c \
  'select $$non repris$$, (select count(*) from parcels) as colis, (select count(*) from clients) as vendeurs, (select count(*) from couriers) as livreurs, (select count(*) from users) - 1 as autres_comptes, (select count(*) from invoices) as factures, (select count(*) from routes) as circuits, (select count(*) from districts where "isActive" = false and "deletedAt" is null) as quartiers_inactifs'
