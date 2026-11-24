#!/usr/bin/env bash
# Reprise, dans la base .NET, des paramètres société et du compte wimo@gmail.com exportés par
# deploy/export-referential.sh (dossier /opt/wimo-delivery/.runtime/export-referentiel).
#  - société : nom, coordonnées, logo ; palette « Bleu Wimo » (#0F62FE / #0A1F44), jamais l'ambre ;
#  - paramètres de portée société (étiquettes, thèmes, finance, tarifs…) : valeurs de production ;
#  - compte wimo@gmail.com : super-admin, empreinte argon2id conservée (le mot de passe ne change pas).
# Idempotent. À lancer SUR le VPS (pas via `bash -s` : psql lit son entrée standard) :
#
#   bash /opt/wimo-delivery/app-dotnet/deploy/import-company.sh
set -euo pipefail
cd /opt/wimo-delivery
set -a; . ./app-dotnet.env; set +a
X=/opt/wimo-delivery/.runtime/export-referentiel
psql_() { docker exec -i wimo-delivery-dotnet-postgres-1 psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -q "$@"; }

psql_ -c 'create schema if not exists import_tmp;
  drop table if exists import_tmp.company, import_tmp.settings, import_tmp.account, import_tmp.zone_rates;
  create table import_tmp.zone_rates ("fromZone" text, "toZone" text, rate numeric, "deliveryDays" int, "isActive" text);
  create table import_tmp.company (name text, "legalName" text, "taxId" text, rc text, address text, city text, phone text, email text,
    website text, "logoUrl" text, "primaryColor" text, "secondaryColor" text, "logoBackground" text, timezone text, "defaultLocale" text);
  create table import_tmp.settings (key text, value text);
  create table import_tmp.account (email text, "firstName" text, "lastName" text, phone text, "passwordHash" text, status text, roles text);'
psql_ -c '\copy import_tmp.company from stdin with (format csv, header true)' <"$X/company.csv"
psql_ -c '\copy import_tmp.settings from stdin with (format csv, header true)' <"$X/settings.csv"
psql_ -c '\copy import_tmp.account from stdin with (format csv, header true)' <"$X/wimo-account.csv"
psql_ -c '\copy import_tmp.zone_rates from stdin with (format csv, header true)' <"$X/zone-rates.csv"

psql_ <<'SQL'
begin;
update companies c set
  name = i.name, "legalName" = nullif(i."legalName", ''), "taxId" = nullif(i."taxId", ''), rc = nullif(i.rc, ''),
  address = nullif(i.address, ''), city = nullif(i.city, ''), phone = nullif(i.phone, ''), email = nullif(i.email, ''),
  website = nullif(i.website, ''), "logoUrl" = nullif(i."logoUrl", ''), "logoBackground" = nullif(i."logoBackground", ''),
  "primaryColor" = '#0F62FE', "secondaryColor" = '#0A1F44', theme = '{}'::jsonb,
  timezone = i.timezone, "defaultLocale" = i."defaultLocale", "updatedAt" = now()
from import_tmp.company i;

update settings s set value = i.value::jsonb, "updatedAt" = now()
from import_tmp.settings i where s.scope = 'COMPANY' and s."agencyId" is null and s.key = i.key;
insert into settings (id, scope, "agencyId", key, value, "createdAt", "updatedAt")
select 'set-' || md5(i.key), 'COMPANY', null, i.key, i.value::jsonb, now(), now() from import_tmp.settings i
where not exists (select 1 from settings s where s.scope = 'COMPANY' and s."agencyId" is null and s.key = i.key);

insert into users (id, "isPlatformAdmin", email, "passwordHash", "firstName", "lastName", phone, "isActive", status,
                   "tokensValidFrom", "mustChangePassword", "createdAt", "updatedAt")
select 'usr-' || md5(lower(a.email)), false, lower(a.email), a."passwordHash", a."firstName", a."lastName", nullif(a.phone, ''),
       true, 'ACTIVE', now(), false, now(), now()
from import_tmp.account a
where not exists (select 1 from users u where lower(u.email) = lower(a.email));
update users u set "passwordHash" = a."passwordHash", "isActive" = true, status = 'ACTIVE', "deletedAt" = null, "updatedAt" = now()
from import_tmp.account a where lower(u.email) = lower(a.email);
insert into user_roles ("userId", role)
select u.id, r.role from import_tmp.account a
join users u on lower(u.email) = lower(a.email)
cross join lateral unnest(string_to_array(a.roles, ';')) as r(role)
on conflict do nothing;

-- Tarifs de zone à zone (référentiel tarifaire, hors import CSV des villes).
insert into zone_rates (id, "fromZoneId", "toZoneId", rate, "deliveryDays", "isActive", "createdAt", "updatedAt")
select 'zrt-' || md5(f.id || t.id), f.id, t.id, r.rate, r."deliveryDays", r."isActive" in ('t', 'true'), now(), now()
from import_tmp.zone_rates r join zones f on f.name = r."fromZone" join zones t on t.name = r."toZone"
on conflict ("fromZoneId", "toZoneId") do update set rate = excluded.rate, "deliveryDays" = excluded."deliveryDays", "isActive" = excluded."isActive", "updatedAt" = now();
commit;
drop schema import_tmp cascade;
SQL

psql_ -At -F' | ' -c "select 'société', name, \"logoUrl\", \"primaryColor\", \"secondaryColor\" from companies"
psql_ -At -F' | ' -c "select 'paramètres société', count(*) from settings where scope = 'COMPANY'"
psql_ -At -F' | ' -c "select 'tarifs de zone', count(*) from zone_rates"
psql_ -At -F' | ' -c "select 'compte', u.email, u.status, string_agg(r.role::text, ',') from users u join user_roles r on r.\"userId\" = u.id where u.email = 'wimo@gmail.com' group by u.email, u.status"
