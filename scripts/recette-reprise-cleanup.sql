-- Nettoyage d'un passage de scripts/recette-reprise.sh : supprime le colis de recette et ce qui s'y rattache (bon,
-- circuit, caisse RF-, événements, notifications, journal), remet aux deux comptes importés leur empreinte, leur
-- indicateur de changement de mot de passe et leur date de validité des jetons d'origine, ferme les sessions ouvertes
-- pendant le passage, puis rend aux compteurs leur valeur d'avant si personne d'autre n'a numéroté. Variables psql :
--   parcel, client_user, courier_user, since, hashes (JSON : ["id|empreinte|doit-changer|jetons-valides-depuis", …]),
--   doc_seq, parcel_seq (instantanés JSON des compteurs).
\set ON_ERROR_STOP on
begin;

create temp table rr_parcels as select id from parcels where id = :'parcel';
create temp table rr_notes as select distinct "deliveryNoteId" id from delivery_note_parcels where "parcelId" in (select id from rr_parcels)
  and "deliveryNoteId" in (select id from delivery_notes where "createdAt" >= :'since'::timestamptz);
create temp table rr_routes as select distinct "routeId" id from route_parcels where "parcelId" in (select id from rr_parcels)
  and "routeId" in (select id from routes where "createdAt" >= :'since'::timestamptz);
create temp table rr_remittances as select distinct "remittanceId" id from cash_remittance_parcels where "parcelId" in (select id from rr_parcels)
  and "remittanceId" in (select id from cash_remittances where "createdAt" >= :'since'::timestamptz);
-- Brouillons de facture créés pendant le passage pour ce vendeur (supprimés par l'API, ceinture et bretelles).
create temp table rr_invoices as select id from invoices where "createdAt" >= :'since'::timestamptz and "clientId" = (select "clientId" from parcels where id = :'parcel')
  and status = 'DRAFT' and not exists (select 1 from legacy_mappings m where m."entity" = 'invoice' and m."newId" = invoices.id);
create temp table foreign_activity as select
  (select count(*) from parcels where "createdAt" >= :'since'::timestamptz and id not in (select id from rr_parcels))
  + (select count(*) from invoices where "createdAt" >= :'since'::timestamptz and id not in (select id from rr_invoices))
  + (select count(*) from routes where "createdAt" >= :'since'::timestamptz and id not in (select id from rr_routes))
  + (select count(*) from delivery_notes where "createdAt" >= :'since'::timestamptz and id not in (select id from rr_notes))
  + (select count(*) from cash_remittances where "createdAt" >= :'since'::timestamptz and id not in (select id from rr_remittances)) as n;

delete from courier_balance_entries where "remittanceId" in (select id from rr_remittances);
delete from cheques where "parcelId" in (select id from rr_parcels) or "remittanceId" in (select id from rr_remittances);
delete from cash_remittance_parcels where "remittanceId" in (select id from rr_remittances) or "parcelId" in (select id from rr_parcels);
delete from cash_remittances where id in (select id from rr_remittances);
delete from invoice_lines where "invoiceId" in (select id from rr_invoices) or "parcelId" in (select id from rr_parcels);
delete from invoice_adjustments where "invoiceId" in (select id from rr_invoices) or "deliveryNoteId" in (select id from rr_notes);
delete from invoices where id in (select id from rr_invoices);
delete from route_notes where "routeId" in (select id from rr_routes) or "deliveryNoteId" in (select id from rr_notes);
delete from route_parcels where "routeId" in (select id from rr_routes) or "parcelId" in (select id from rr_parcels);
delete from routes where id in (select id from rr_routes);
delete from delivery_note_parcels where "deliveryNoteId" in (select id from rr_notes) or "parcelId" in (select id from rr_parcels);
delete from delivery_note_removed_parcels where "deliveryNoteId" in (select id from rr_notes) or "parcelId" in (select id from rr_parcels);
delete from delivery_notes where id in (select id from rr_notes);
delete from courier_statement_lines where "parcelId" in (select id from rr_parcels);
delete from parcel_events where "parcelId" in (select id from rr_parcels);
delete from parcel_notes where "parcelId" in (select id from rr_parcels);
delete from parcel_fee_changes where "parcelId" in (select id from rr_parcels);
delete from parcel_items where "parcelId" in (select id from rr_parcels);
delete from parcels where id in (select id from rr_parcels);

delete from notifications where "createdAt" >= :'since'::timestamptz and "userId" in (:'client_user', :'courier_user');
delete from refresh_sessions where "createdAt" >= :'since'::timestamptz and "userId" in (:'client_user', :'courier_user');
delete from audit_logs where at >= :'since'::timestamptz
  and ("userId" in (:'client_user', :'courier_user') or "entityId" in (:'client_user', :'courier_user')
       or "entityId" in (select id from rr_parcels union all select id from rr_invoices union all select id from rr_routes
                         union all select id from rr_notes union all select id from rr_remittances));
update users u set "passwordHash" = split_part(h, '|', 2), "mustChangePassword" = split_part(h, '|', 3)::boolean,
  "tokensValidFrom" = split_part(h, '|', 4)::timestamptz
from json_array_elements_text(:'hashes'::json) h where u.id = split_part(h, '|', 1);

delete from document_sequences d where (select n from foreign_activity) = 0
  and not exists (select 1 from json_to_recordset(:'doc_seq'::json) s(kind text, period text, "lastRank" int) where s.kind = d.kind and s.period = d.period);
update document_sequences d set "lastRank" = s."lastRank" from json_to_recordset(:'doc_seq'::json) s(kind text, period text, "lastRank" int)
  where (select n from foreign_activity) = 0 and s.kind = d.kind and s.period = d.period;
delete from parcel_code_sequences d where (select n from foreign_activity) = 0
  and not exists (select 1 from json_to_recordset(:'parcel_seq'::json) s(day text, "originCityId" text, "destinationCityId" text, "lastRank" int)
                  where s.day = d.day::text and s."originCityId" = d."originCityId" and s."destinationCityId" = d."destinationCityId");
update parcel_code_sequences d set "lastRank" = s."lastRank"
  from json_to_recordset(:'parcel_seq'::json) s(day text, "originCityId" text, "destinationCityId" text, "lastRank" int)
  where (select n from foreign_activity) = 0 and s.day = d.day::text and s."originCityId" = d."originCityId" and s."destinationCityId" = d."destinationCityId";

select 'nettoyage', (select count(*) from rr_parcels) as colis, (select count(*) from rr_routes) as circuits, (select count(*) from rr_remittances) as caisses,
  case when (select n from foreign_activity) = 0 then 'compteurs restaures' else 'compteurs conserves (activite pendant la recette)' end;
commit;
