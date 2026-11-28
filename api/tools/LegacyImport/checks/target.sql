-- Contrôles d'argent de la reprise, côté Wimo .NET (PostgreSQL) : mêmes lignes « contrôle|clé|valeur » que
-- source.sql, clés = identifiants Laravel (legacy_mappings), sur les seules lignes reprises.
\pset format unaligned
\pset tuples_only on

SELECT 'client-factures|' || mc."legacyId" || '|' || COUNT(*) || ';' || to_char(SUM(i."totalCod"), 'FM999,999,999,990.00') || ';'
  || to_char(SUM(i."totalDeliveryFee" + i."totalFailureFee"), 'FM999,999,999,990.00') || ';' || to_char(SUM(i."totalDue"), 'FM999,999,999,990.00')
FROM invoices i
JOIN legacy_mappings mi ON mi."entity" = 'invoice' AND mi."newId" = i.id
JOIN legacy_mappings mc ON mc."entity" = 'client' AND mc."newId" = i."clientId"
WHERE EXISTS (SELECT 1 FROM invoice_lines l WHERE l."invoiceId" = i.id)
GROUP BY mc."legacyId";

SELECT 'client-factures-sans-colis|' || mc."legacyId" || '|' || COUNT(*)
FROM invoices i
JOIN legacy_mappings mi ON mi."entity" = 'invoice' AND mi."newId" = i.id
JOIN legacy_mappings mc ON mc."entity" = 'client' AND mc."newId" = i."clientId"
WHERE NOT EXISTS (SELECT 1 FROM invoice_lines l WHERE l."invoiceId" = i.id)
GROUP BY mc."legacyId";

SELECT 'client-reglements|' || mc."legacyId" || '|' || COUNT(*) || ';' || to_char(SUM(p.amount), 'FM999,999,999,990.00')
FROM payments p
JOIN legacy_mappings mp ON mp."entity" = 'payment' AND mp."newId" = p.id
JOIN invoices i ON i.id = p."invoiceId"
JOIN legacy_mappings mc ON mc."entity" = 'client' AND mc."newId" = i."clientId"
GROUP BY mc."legacyId";

SELECT 'client-colis|' || mc."legacyId" || '|' || COUNT(*) || ';' || to_char(SUM(p."codAmount"), 'FM999,999,999,990.00') || ';'
  || to_char(SUM(p."deliveryFee"), 'FM999,999,999,990.00') || ';' || to_char(SUM(COALESCE(p."failureFee", 0)), 'FM999,999,999,990.00')
FROM parcels p
JOIN legacy_mappings mp ON mp."entity" = 'parcel' AND mp."newId" = p.id
JOIN legacy_mappings mc ON mc."entity" = 'client' AND mc."newId" = p."clientId"
GROUP BY mc."legacyId";

SELECT 'livreur-recettes|' || mk."legacyId" || '|' || to_char(SUM(l.amount), 'FM999,999,999,990.00') || ';' || to_char(SUM(l.commission), 'FM999,999,999,990.00') || ';'
  || to_char(SUM(l.amount - l.commission), 'FM999,999,999,990.00')
FROM cash_remittance_parcels l
JOIN cash_remittances r ON r.id = l."remittanceId"
JOIN legacy_mappings mr ON mr."entity" = 'cash_remittance' AND mr."newId" = r.id
JOIN legacy_mappings mk ON mk."entity" = 'courier' AND mk."newId" = r."courierId"
JOIN parcels p ON p.id = l."parcelId"
WHERE p."statusId" = 4
GROUP BY mk."legacyId";

SELECT 'livreur-recettes-nombre|' || mk."legacyId" || '|' || COUNT(*)
FROM cash_remittances r
JOIN legacy_mappings mr ON mr."entity" = 'cash_remittance' AND mr."newId" = r.id
JOIN legacy_mappings mk ON mk."entity" = 'courier' AND mk."newId" = r."courierId"
GROUP BY mk."legacyId";

SELECT 'livreur-factures|' || mk."legacyId" || '|' || COUNT(DISTINCT s.id) || ';' || to_char(SUM(l.amount), 'FM999,999,999,990.00')
FROM courier_statements s
JOIN legacy_mappings ms ON ms."entity" = 'courier_statement' AND ms."newId" = s.id
JOIN courier_statement_lines l ON l."statementId" = s.id
JOIN legacy_mappings mk ON mk."entity" = 'courier' AND mk."newId" = s."courierId"
GROUP BY mk."legacyId";

SELECT 'colis-par-statut|' || p."statusId" || '|' || COUNT(*)
FROM parcels p
JOIN legacy_mappings mp ON mp."entity" = 'parcel' AND mp."newId" = p.id
GROUP BY p."statusId";
