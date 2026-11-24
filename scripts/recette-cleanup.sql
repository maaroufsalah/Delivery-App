-- Nettoyage d'un passage de scripts/recette.sh : supprime EXACTEMENT ce que ce passage a créé
-- (vendeur, livreur, leurs comptes, colis, bons, circuits, caisse, facture, réclamation, type de
-- réclamation, notifications, journal), puis rend aux compteurs de numérotation leur valeur d'avant
-- le passage si personne d'autre n'a numéroté entre-temps. Variables psql :
--   client, courier, merchant_user, courier_user, claim_type, since (horodatage ISO du début),
--   doc_seq, parcel_seq (instantanés JSON des compteurs avant le passage).
\set ON_ERROR_STOP on
begin;

create temp table rx_users as select id from users where id in (:'merchant_user', :'courier_user');
create temp table rx_parcels as select id from parcels where "clientId" = :'client' or "courierId" = :'courier';
create temp table rx_claims as select id from claims where "clientId" = :'client' or "parcelId" in (select id from rx_parcels);
create temp table rx_invoices as select id from invoices where "clientId" = :'client';
create temp table rx_routes as select id from routes where "courierId" = :'courier';
create temp table rx_notes as select id from delivery_notes where "clientId" = :'client';
create temp table rx_remittances as select id from cash_remittances where "courierId" = :'courier';

-- Activité étrangère pendant le passage : si quelqu'un a numéroté, les compteurs ne reculent pas.
create temp table foreign_activity as select
  (select count(*) from parcels where "createdAt" >= :'since'::timestamptz and id not in (select id from rx_parcels))
  + (select count(*) from invoices where "createdAt" >= :'since'::timestamptz and id not in (select id from rx_invoices))
  + (select count(*) from claims where "createdAt" >= :'since'::timestamptz and id not in (select id from rx_claims))
  + (select count(*) from routes where "createdAt" >= :'since'::timestamptz and id not in (select id from rx_routes))
  + (select count(*) from delivery_notes where "createdAt" >= :'since'::timestamptz and id not in (select id from rx_notes))
  + (select count(*) from cash_remittances where "createdAt" >= :'since'::timestamptz and id not in (select id from rx_remittances)) as n;

delete from claim_events where "claimId" in (select id from rx_claims);
delete from claim_attachments where "claimId" in (select id from rx_claims);
delete from claims where id in (select id from rx_claims);
delete from claim_types where id = :'claim_type' and not exists (select 1 from claims where "typeId" = :'claim_type');

delete from courier_balance_entries where "courierId" = :'courier' or "remittanceId" in (select id from rx_remittances);
delete from cheques where "parcelId" in (select id from rx_parcels) or "remittanceId" in (select id from rx_remittances) or "invoiceId" in (select id from rx_invoices);
delete from cash_remittance_parcels where "remittanceId" in (select id from rx_remittances) or "parcelId" in (select id from rx_parcels);
delete from cash_remittances where id in (select id from rx_remittances);

delete from invoice_adjustments where "invoiceId" in (select id from rx_invoices) or "deliveryNoteId" in (select id from rx_notes);
delete from invoice_lines where "invoiceId" in (select id from rx_invoices) or "parcelId" in (select id from rx_parcels);
delete from payments where "invoiceId" in (select id from rx_invoices) or "clientId" = :'client';
delete from invoices where id in (select id from rx_invoices);

delete from route_notes where "routeId" in (select id from rx_routes) or "deliveryNoteId" in (select id from rx_notes);
delete from route_parcels where "routeId" in (select id from rx_routes) or "parcelId" in (select id from rx_parcels);
delete from routes where id in (select id from rx_routes);
delete from delivery_note_parcels where "deliveryNoteId" in (select id from rx_notes) or "parcelId" in (select id from rx_parcels);
delete from delivery_note_removed_parcels where "deliveryNoteId" in (select id from rx_notes) or "parcelId" in (select id from rx_parcels);
delete from delivery_notes where id in (select id from rx_notes);

delete from courier_statement_lines where "parcelId" in (select id from rx_parcels);
delete from courier_statements where "courierId" = :'courier';
delete from parcel_events where "parcelId" in (select id from rx_parcels);
delete from parcel_notes where "parcelId" in (select id from rx_parcels);
delete from parcel_fee_changes where "parcelId" in (select id from rx_parcels);
delete from parcel_items where "parcelId" in (select id from rx_parcels);
delete from parcels where id in (select id from rx_parcels);

delete from notifications where "userId" in (select id from rx_users)
  or ("createdAt" >= :'since'::timestamptz and (data ->> 'claimCode') is not null and not exists (select 1 from claims c where c.code = data ->> 'claimCode'));
delete from push_tokens where "userId" in (select id from rx_users);
delete from refresh_sessions where "userId" in (select id from rx_users);
delete from notification_preferences where "userId" in (select id from rx_users);
delete from user_agencies where "userId" in (select id from rx_users);
delete from user_permission_overrides where "userId" in (select id from rx_users);
delete from user_roles where "userId" in (select id from rx_users);
delete from courier_cities where "courierId" = :'courier';
delete from couriers where id = :'courier';
delete from client_discounts where "clientId" = :'client';
delete from client_pickup_addresses where "clientId" = :'client';
delete from clients where id = :'client';
delete from audit_logs where at >= :'since'::timestamptz
  and ("userId" in (select id from rx_users) or "subjectClientId" = :'client'
       or "entityId" in (:'client', :'courier', :'merchant_user', :'courier_user', :'claim_type')
       or "entityId" in (select id from rx_parcels union all select id from rx_claims union all select id from rx_invoices
                         union all select id from rx_routes union all select id from rx_notes union all select id from rx_remittances));
delete from users where id in (select id from rx_users);

-- Compteurs : retour à l'instantané d'avant le passage, sauf activité étrangère.
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

select 'nettoyage', (select count(*) from rx_parcels) as colis, (select count(*) from rx_invoices) as factures, (select count(*) from rx_claims) as reclamations,
  case when (select n from foreign_activity) = 0 then 'compteurs restaures' else 'compteurs conserves (activite pendant la recette)' end;
commit;
