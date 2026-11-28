-- Contrôles d'argent de la reprise, côté Wimo Laravel (MariaDB, lecture seule). Une ligne « contrôle|clé|valeur ».
-- Formules de Laravel réécrites en SQL, indépendantes de l'outil d'import :
--   facture client : App\Http\Resources\Facture (colisLivres, colisLivresFraisNonInclu, colisSumFrais,
--   colisSumFraisEchoue, colisSumFraisPayeEspece) ; recette : LivreurFactureService::calculer (colis livrés,
--   COD = Colis::montantAEncaisser, frais = première ligne livreur_ville tarifée) ; facture livreur : somme des
--   frais_livreur de ses lignes d'historique.
SET SESSION TRANSACTION READ ONLY;
START TRANSACTION READ ONLY;

WITH invoice_rows AS (
  SELECT f.id facture_id, f.client_id,
    CASE WHEN c.statut_id = 4 AND c.frais_echoue IS NULL THEN
      (CASE WHEN c.deja_paye != 1 THEN CAST(COALESCE(NULLIF(c.prix, ''), '0') AS DECIMAL(14,2)) ELSE 0 END)
      + (CASE WHEN c.frais_inclus != 1 THEN COALESCE(c.frais, 0) ELSE 0 END) ELSE 0 END AS cod,
    CASE WHEN c.statut_id = 4 AND c.frais_echoue IS NULL THEN COALESCE(c.frais, 0) ELSE 0 END AS fee,
    COALESCE(c.frais_echoue, 0) AS failure,
    CASE WHEN c.statut_id = 4 AND c.frais_deja_paye = 1 THEN COALESCE(c.frais, 0) ELSE 0 END AS cash
  FROM factures f
  JOIN facture_colis fc ON fc.facture_id = f.id
  JOIN colis c ON c.id = fc.colis_id AND c.deleted_at IS NULL
  WHERE f.client_id IS NOT NULL
)
SELECT CONCAT('client-factures|', client_id, '|', COUNT(DISTINCT facture_id), ';', FORMAT(SUM(cod), 2, 'en_US'), ';',
  FORMAT(SUM(fee - cash + failure), 2, 'en_US'), ';', FORMAT(SUM(cod - fee - failure + cash), 2, 'en_US'))
FROM invoice_rows GROUP BY client_id;

SELECT CONCAT('client-factures-sans-colis|', f.client_id, '|', COUNT(*))
FROM factures f
WHERE f.client_id IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM facture_colis fc JOIN colis c ON c.id = fc.colis_id AND c.deleted_at IS NULL WHERE fc.facture_id = f.id)
GROUP BY f.client_id;

SELECT CONCAT('client-reglements|', f.client_id, '|', COUNT(*), ';', FORMAT(SUM(r.montant), 2, 'en_US'))
FROM rembourssements r JOIN factures f ON f.id = r.facture_id
WHERE f.client_id IS NOT NULL GROUP BY f.client_id;

SELECT CONCAT('client-colis|', c.client_id, '|', COUNT(*), ';', FORMAT(SUM(CAST(COALESCE(NULLIF(c.prix, ''), '0') AS DECIMAL(14,2))), 2, 'en_US'), ';',
  FORMAT(SUM(COALESCE(c.frais, 0)), 2, 'en_US'), ';', FORMAT(SUM(COALESCE(c.frais_echoue, 0)), 2, 'en_US'))
FROM colis c WHERE c.deleted_at IS NULL GROUP BY c.client_id;

WITH grid AS (
  SELECT lv.livreur_id, lv.ville_id, lv.frais_livraison FROM livreur_ville lv
  WHERE lv.frais_livraison IS NOT NULL
    AND lv.id = (SELECT MIN(x.id) FROM livreur_ville x WHERE x.livreur_id = lv.livreur_id AND x.ville_id = lv.ville_id AND x.frais_livraison IS NOT NULL)
), remittance_rows AS (
  SELECT r.partenaire_id livreur_id,
    CASE WHEN COALESCE(c.frais_inclus, 0) != 0 THEN
           (CASE WHEN CAST(COALESCE(NULLIF(c.prix, ''), '0') AS DECIMAL(14,2)) != 0 AND COALESCE(c.deja_paye, 0) = 0 THEN CAST(c.prix AS DECIMAL(14,2)) ELSE 0 END)
         WHEN COALESCE(c.frais, 0) != 0 THEN
           (CASE WHEN CAST(COALESCE(NULLIF(c.prix, ''), '0') AS DECIMAL(14,2)) != 0 AND COALESCE(c.deja_paye, 0) = 0 THEN CAST(c.prix AS DECIMAL(14,2)) + c.frais ELSE c.frais END)
         ELSE 0 END AS cod,
    COALESCE(g.frais_livraison, 0) AS commission
  FROM recettes r
  JOIN recette_colis rc ON rc.recette_id = r.id
  JOIN colis c ON c.id = rc.colis_id AND c.deleted_at IS NULL AND c.statut_id = 4
  LEFT JOIN grid g ON g.livreur_id = r.partenaire_id AND g.ville_id = c.livraison_ville_id
  WHERE r.partenaire_type = 'App\\Livreur'
)
SELECT CONCAT('livreur-recettes|', livreur_id, '|', FORMAT(SUM(cod), 2, 'en_US'), ';', FORMAT(SUM(commission), 2, 'en_US'), ';',
  FORMAT(SUM(cod - commission), 2, 'en_US'))
FROM remittance_rows GROUP BY livreur_id;

SELECT CONCAT('livreur-recettes-nombre|', partenaire_id, '|', COUNT(*))
FROM recettes WHERE partenaire_type = 'App\\Livreur' GROUP BY partenaire_id;

SELECT CONCAT('livreur-factures|', f.livreur_id, '|', COUNT(DISTINCT f.id), ';', FORMAT(SUM(COALESCE(t.frais_livreur, 0)), 2, 'en_US'))
FROM factures f
JOIN facture_livreur_colis fl ON fl.facture_id = f.id
JOIN colis_trakings t ON t.id = fl.traking_id
WHERE f.livreur_id IS NOT NULL AND f.client_id IS NULL GROUP BY f.livreur_id;

SELECT CONCAT('colis-par-statut|', s, '|', COUNT(*)) FROM (
  SELECT CASE c.statut_id WHEN 15 THEN 16 WHEN 30 THEN 7 WHEN 36 THEN 33 ELSE COALESCE(c.statut_id, 1) END s
  FROM colis c WHERE c.deleted_at IS NULL
) x GROUP BY s;

COMMIT;
