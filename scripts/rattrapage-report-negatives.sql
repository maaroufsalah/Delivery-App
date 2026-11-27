-- RATTRAPAGE « Report des factures négatives » — les factures négatives « Payée » deviennent « Reportée ».
--
-- Avant la règle, une facture à net < 0 d'un vendeur ordinaire pouvait recevoir un règlement « Espèces » du même
-- montant : un règlement FICTIF, et un montant jamais déduit. Ce script les remet dans la règle (même sélection que
-- `CarryOver.Applies` : ni Client Pro, ni client interne, ni régime PROFESSIONAL) :
--   - le règlement Espèces est ANNULÉ (cancelledAt, motif), jamais supprimé ;
--   - la facture passe CARRIED_OVER, carriedIntoId nul : la PROCHAINE facture émise du vendeur l'absorbera
--     (ligne « Report FA-… : −X MAD »). Lignes et montants ne bougent pas.
-- Une facture n'est retenue que si elle est COHÉRENTE : un seul règlement actif, Espèces, du montant exact ; aucune
-- ligne de report ne la cite ; aucun OV ne la porte. Tout ou rien, chaque écriture tracée dans audit_logs.
--
-- À lancer APRÈS le déploiement du code qui connaît CARRIED_OVER (migration CarryOverNegativeInvoices appliquée),
-- et après un dump de la base.
--   Simulation (défaut, ROLLBACK) : psql "$DATABASE_URL" -f scripts/rattrapage-report-negatives.sql
--   Application                   : psql "$DATABASE_URL" -v apply=1 -v expect=<n>:<total MAD> -f scripts/rattrapage-report-negatives.sql
-- Rejoué après application : « 0 facture ».

\set ON_ERROR_STOP on
\if :{?apply}
\else
  \set apply 0
\endif
\if :{?expect}
\else
  \set expect ''
\endif

BEGIN;

CREATE TEMP TABLE rattrapage ON COMMIT DROP AS
SELECT i.id, i.code, i."clientId", c.name AS client, i."totalDue" AS due, i."paidAt", i."paidById",
       p.id AS payment_id, p.method, p.amount, p."paidAt" AS payment_at,
       concat_ws(' ; ',
         CASE WHEN active.n = 0 THEN 'aucun règlement actif' END,
         CASE WHEN active.n > 1 THEN active.n || ' règlements actifs' END,
         CASE WHEN p.method IS NOT NULL AND p.method <> 'CASH' THEN 'règlement ' || p.method || ', pas Espèces' END,
         CASE WHEN p.amount IS NOT NULL AND p.amount <> -i."totalDue" THEN 'règlement ' || p.amount || ' ≠ |net| ' || -i."totalDue" END,
         CASE WHEN EXISTS (SELECT 1 FROM invoice_adjustments a WHERE a."carriedFromInvoiceId" = i.id) THEN 'déjà citée par une ligne de report' END,
         (SELECT 'portée par ' || o.code FROM transfer_orders o WHERE i.code = ANY (o."invoiceCodes") LIMIT 1)
       ) AS problems,
       (SELECT d.code FROM invoices d WHERE d."clientId" = i."clientId" AND d.status = 'DRAFT' LIMIT 1) AS draft
FROM invoices i
JOIN clients c ON c.id = i."clientId"
CROSS JOIN LATERAL (SELECT count(*) AS n FROM payments x WHERE x."invoiceId" = i.id AND x."cancelledAt" IS NULL) active
LEFT JOIN LATERAL (SELECT * FROM payments x WHERE x."invoiceId" = i.id AND x."cancelledAt" IS NULL ORDER BY x."paidAt" LIMIT 1) p ON true
WHERE i.status = 'PAID' AND i."totalDue" < 0
  AND NOT c."isPro" AND NOT c."isInternal" AND i."billingType" <> 'PROFESSIONAL'
ORDER BY i.code;

\echo 'Factures négatives « Payée » d''un vendeur ordinaire :'
SELECT code, client, due AS net, method AS reglement, amount AS montant, payment_at::date AS le,
       CASE WHEN problems = '' THEN 'à reporter' ELSE 'ÉCART : ' || problems END AS verdict
FROM rattrapage ORDER BY code;

\echo 'Total à reporter par vendeur (déduit de sa prochaine facture émise) :'
SELECT client, -sum(due) AS a_reporter, string_agg(code, ', ' ORDER BY code) AS factures, coalesce(max(draft), 'aucun') AS brouillon
FROM rattrapage GROUP BY "clientId", client ORDER BY client;

SELECT count(*) AS factures, coalesce(-sum(due), 0) AS total_mad FROM rattrapage;

SELECT set_config('rattrapage.apply', :'apply', true), set_config('rattrapage.expect', :'expect', true);

DO $$
DECLARE
  apply boolean := current_setting('rattrapage.apply') = '1';
  expect text := current_setting('rattrapage.expect');
  n int; total numeric; wrong int;
  now_at timestamptz := date_trunc('milliseconds', now());
  reason text := 'Règlement fictif : facture à net négatif, reportée sur la prochaine facture du vendeur.';
  r record;
BEGIN
  SELECT count(*), coalesce(-sum(due), 0), count(*) FILTER (WHERE problems <> '') INTO n, total, wrong FROM rattrapage;
  IF wrong > 0 THEN RAISE NOTICE 'RIEN N''EST APPLIQUÉ : % facture(s) incohérente(s).', wrong; RETURN; END IF;
  IF NOT apply THEN RAISE NOTICE 'Simulation seule (relancer avec -v apply=1 -v expect=%:%).', n, total; RETURN; END IF;
  IF expect = '' OR split_part(expect, ':', 1)::int <> n OR split_part(expect, ':', 2)::numeric <> total THEN
    RAISE EXCEPTION 'RIEN N''EST APPLIQUÉ : attendu %, trouvé %:%.', nullif(expect, ''), n, total;
  END IF;
  FOR r IN SELECT * FROM rattrapage ORDER BY code LOOP
    UPDATE payments SET "cancelledAt" = now_at, "cancelledById" = NULL, "cancelReason" = reason, "updatedAt" = now_at WHERE id = r.payment_id;
    INSERT INTO audit_logs (id, entity, "entityId", action, before, after, diff, "subjectClientId", "userEmail", role, source, "requestId", at)
    VALUES ('rpt' || substr(md5(random()::text || r.payment_id), 1, 22), 'Payment', r.payment_id, 'UPDATE',
            jsonb_build_object('invoiceCode', r.code, 'method', r.method, 'amount', r.amount::text, 'cancelledAt', NULL),
            jsonb_build_object('invoiceCode', r.code, 'method', r.method, 'amount', r.amount::text, 'cancelledAt', to_char(now_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'), 'cancelReason', reason),
            ARRAY['cancelledAt', 'cancelReason'], r."clientId", 'rattrapage-report-negatives@systeme.invalid', 'SYSTEM', 'manual', 'manual-rattrapage-report-negatives', now_at);
    -- Le statut seul : lignes et montants de la facture émise ne bougent pas.
    UPDATE invoices SET status = 'CARRIED_OVER', "paidAt" = NULL, "paidById" = NULL, "updatedAt" = now_at WHERE id = r.id;
    INSERT INTO audit_logs (id, entity, "entityId", action, before, after, diff, "subjectClientId", "userEmail", role, source, "requestId", at)
    VALUES ('rpt' || substr(md5(random()::text || r.id), 1, 22), 'Invoice', r.id, 'UPDATE',
            jsonb_build_object('code', r.code, 'status', 'PAID', 'paidAt', to_char(r."paidAt" AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'), 'paidById', r."paidById"),
            jsonb_build_object('code', r.code, 'status', 'CARRIED_OVER', 'paidAt', NULL, 'paidById', NULL),
            ARRAY['status', 'paidAt', 'paidById'], r."clientId", 'rattrapage-report-negatives@systeme.invalid', 'SYSTEM', 'manual', 'manual-rattrapage-report-negatives', now_at);
  END LOOP;
  RAISE NOTICE 'APPLIQUÉ : % facture(s) reportée(s), % règlement(s) annulé(s), % MAD.', n, n, total;
END $$;

\if :apply
COMMIT;
\else
ROLLBACK;
\endif
