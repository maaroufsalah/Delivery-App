# CHECKPOINT — chantier « Delivery-App » (API .NET pour Wimo)

Rapport final : `docs/chantiers/delivery-app-dotnet.md` (en fin de chantier). Checklist : `docs/contract/INVENTAIRE.md`
(`node scripts/check-inventory.mjs` coche les routes servies par les contrôleurs .NET).

## Fait

- **PR 1 socle** (#1, fusionnée) : web/mobile/packages copiés de Speedy `main` e9a98b06, inventaire
  506 endpoints (494 dans le périmètre), solution .NET 10, 104 entités EF (`scripts/prisma-to-ef.py`,
  `companyId` retiré), migration `InitialSchema`, erreurs au format Nest, JSON `.fffZ`, CI.
- **PR 2 identité** (#2, fusionnée) : 27/27 routes du module 2 (auth, sessions/rotation, cookies
  `dp.at`/`dp.rt`, comptes, rôles, écarts, inscription publique, frein, health). 20 tests unitaires,
  45 tests de contrat verts. Seed : société `cmp-wimo`, permissions, super-admin `SEED_ADMIN_*`.

## Fait (suite)

- **PR 3 référentiel** (#3, fusionnée) : 68 routes (villes, zones, quartiers, agences, types, tarifs,
  directory, settings, public), seed statuts/Casablanca, import CSV, logo SkiaSharp.

- **PR 4 comptes** (#4, fusionnée) : 35 routes clients/livreurs/merchant company. Journal d'audit
  générique (`AuditInterceptor`, 64 entités) commité en tête de `feat/parcels`.

## Fait — module 5 « Colis » (#5, fusionnée)

90 routes (220/494 cochées) : colis (liste, fiche, création idempotente, statut, frais, destination,
vendeur, admin, client, BL, notes), échanges, étiquettes PDF + label-jobs, suivi public, import Excel,
entrepôt (produits, stock, entreposages + bon de réception PDF, commandes, imports), jetons
prestataires, audit technique + journal métier, merchant/me/summary, courier/me. 78 tests de contrat,
44 unitaires. Rangées au module 6 (remap `gen-inventory.mjs`) : `courier/me/{notes,routes,deliveries,
scan,performance}`, `courier/parcels/:code/*`, `courier-stock`, `provider/parcels/:code/status`,
`bulk-jobs` ; au module 8 : `courier/me/{cheques,balance,remittance}`.

**Écarts à reporter dans le rapport final** : journal sans la reprise des anciens `UPDATE_MANY`
(aucune ligne de ce type n'est écrite ici) ; mise en page des étiquettes et du bon de réception
reprise à l'identique en contenu, rendu QuestPDF (à vérifier en recette) ; logo de marque non
« assombri » (`logoForLightBackground`, corrigé au module 6).

**Bugs Speedy corrigés côté .NET, à corriger aussi dans Speedy** (règle : un bug d'argent se corrige,
jamais de reproduction) : `POST /exchanges` passait `amount` (centimes) comme COD en dirhams (COD ×100) ;
le COD du nouveau colis vaut désormais `amount / 100` MAD (`ExchangeWrites.CodMadOf`, test unitaire
15050 → 150,50 MAD, test de contrat).

**Dettes à solder au module 6** : statut 13 « En cours de livraison » (500 « Mise en circuit
indisponible », comme Speedy sans `OutForDeliveryPlacer`) ; `replayAutoClose` après changement de
destination. Module 10 : envoi push (Expo) des notifications.

## Fait — module 6 « Bons et circuits » (#6, fusionnée)

Lots : (A) bons BR-/BD- + demandes vendeur + tâches de masse — FAIT (commit `feat(notes)…`) ;
(B) circuits : liste, stats, export, fiche, création, modif, suppression, candidats, ajout/retrait de
colis et de bons, ramassage d'un bon ; (C) départ, confirmation des issues (encaissement, commission,
retour de fonds, chèque), clôture ; (D) statut 13 (`OutForDeliveryPlacer`, test de contrat qui prouve
la fin du 500), correction de statut, annulation de livraison, `field-status`, transfert ;
(E) `courier/*`, `courier-stock`, statut prestataire, `staff/me/alerts` ; (F) PDF fidèles (étiquettes,
reçu d'entreposage, feuille de route, bon) comparés dans `docs/design/labels/`.
État : lots (A) à (E) FAITS sur `feat/routes` (non poussée) — 281/494 routes, tests de contrat verts.
- (B)(C) circuits : composition, départ, issues (encaissement espèces/chèque en centimes, commission,
  ligne RF- automatique), clôture et clôture automatique.
- (D) statut 13 placé sur un circuit (fin du 500, test de contrat) ; annulation de livraison et
  correction de statut : argent, commission et ligne RF- défaits dans LA MÊME transaction, journal
  d'audit, test du montant avant/après ; statut de terrain admin ; transferts entre livreurs.
- (E) espace livreur (`courier/*`, rejeu `Idempotency-Key` sous verrou consultatif de session),
  prestataires (`provider/parcels/:code/status`, garde par jeton, débit compté en base), alertes staff,
  stock livreur (liste, détail, exports).
- Ajustements Salah du 24/11 : index unique partiel `cash_remittances_one_open_per_courier` + verrou
  `pg_advisory_xact_lock` par livreur + test de concurrence (4 livraisons simultanées → 1 RF-) ;
  « jour » via `ClockDays` (IClock, Africa/Casablanca) ; liste de tâches à chaque début/fin de module.

Écarts notés pour le rapport final :
- Bugs Speedy corrigés (argent) : POST /exchanges (centimes/dirhams) ; `collected()` du livreur et du
  stock ignorait « déjà payé » → `codToCollect` (performance, stock livreur).
- Bug Speedy corrigé (module 7, demande de Salah) : la photo de preuve du dernier colis d'un circuit
  tombait en 404 (circuit clos automatiquement) ; elle s'attache désormais au colis du livreur.
- Reproduit (sans impact financier) : `GET /courier/me/routes?status=closed` → `[]`.
- Choix d'infrastructure : débit des prestataires compté sur `provider_calls` (fenêtre glissante,
  multi-instance) au lieu d'un compteur en mémoire ; rejeu idempotent sérialisé par
  `pg_advisory_lock` de session au lieu d'une attente en mémoire.
- Poste Windows : la base de fuseaux Windows donne UTC+0 au Maroc d'octobre 2026 à mars 2027
  (faux) ; tzdata Linux (VPS, CI) est juste. Les tests de fuseau utilisent une date de juin.
- À brancher plus tard : recalcul des relevés livreur brouillons après une annulation de livraison
  (module 8).

Module 6 TERMINÉ sur `feat/routes` : PR #6 ouverte (https://github.com/maaroufsalah/Delivery-App/pull/6),
289/494 routes, 55 tests unitaires et 91 tests de contrat verts en local. Lot (F) : PDF alignés sur
Speedy, comparaison et écarts mesurés dans `docs/design/labels/README.md`. Fichiers éclatés un par cas
d'usage (`scripts/split-module.py`) ; balayage de portée `RoutesScopeSweepContractTests`.

## Fait — module 7 « Retours et BL » (#7, fusionnée)

54 routes : `bl/*` (19), `return-handovers/*` (19), `return-slips/*` (14), `returns/pending` (+ export).
343/494 routes cochées. 56 tests unitaires et 99 tests de contrat verts en local.
- BL : récupéré à la livraison (`collectOnDelivery` branché dans `CourierParcels.ReportAsync`, un refus
  n'annule jamais la livraison), bons BRB- (brouillon → remis → reçu → rendu au vendeur → clos),
  avance en masse et retour arrière administrateur, notifications `bl-ready`, `bl-returned`,
  `bl-note-awaiting` (personnel détenteur de `returns.confirm`, `Notifier.NotifyStaffAsync`).
- Bons de retour livreur RL- : ouverture livreur ou agence, scan, prise en charge, remise, réception
  (colis → 22, clôture automatique du circuit), clôture, annulation (relâche les colis), masse
  idempotente sous `pg_advisory_xact_lock`.
- Bons de retour client BRT- : colis rentrés en agence, frais d'échec de la politique de l'agence,
  remise (colis → 23, frais d'échec posés sur un colis en échec qui n'en avait pas, test avant/après).
- Masque livreur : `/api/bl/notes/:code/pdf` et `/api/return-slips/:code/pdf` imprimés par un vendeur
  passent par `CourierMasks.ForReaderAsync`.
- PDF BRB-, RL-, BRT- comparés à Speedy dans `docs/design/labels/` (identiques).
- Reproduit (sans impact financier) : à la remise d'un bon BRT-, seuls le nom et le type de la pièce
  jointe sont gardés, pas son contenu (Speedy fait de même).
- Balayage de portée : `ReturnsContractTests.Returns_stay_within_their_owner`.

## Fait — module 8 « Caisse et chèques » (#8, fusionnée)

54 des 60 routes du module ; 397/494 cochées. 56 tests unitaires et 108 tests de contrat verts.
- Retours de fonds RF- : liste par étape (ouverte, fermée, reçue, validée), cartes de synthèse,
  candidats, création manuelle (colis obligatoires, un seul bon ouvert par livreur), comptage avec
  motif d'écart, confirmation (lignes figées, colis « remis livreur », chèques reçus, écart porté au
  solde du livreur, le tout en une transaction), réouverture, reçu PDF. Côté livreur : sa caisse,
  sa clôture, son solde. Solde et règlements d'écart côté agence.
- Chèques : liste et somme, fiche, parcours (livreur → agence → vendeur), avance en masse, rejet
  motivé, annulation ; bons de remise BRC- (un chèque, ou groupé par vendeur, logo assombri).
- Tableau financier, synthèse (sept blocs), banques et comptes.
- Petite caisse : mouvements, journées chaînées (solde d'ouverture et de clôture), clôture manuelle
  ou automatique (tâche de fond chaque minute, inactive en test), réouverture motivée, dépenses du
  livreur en brouillon, exports et PDF.
- Reportés au module 9 (ils lisent les relevés et les factures) : `GET/POST /courier/me/cheques`,
  `payments` (4 routes), et le rejeu des lignes de facture brouillon après un chèque rejeté ou
  annulé (`InvoiceDraftReplay`, à compléter avant toute facture brouillon).
- Reproduit (sans impact financier) : un statut de chèque inconnu dans le filtre ne sélectionne rien
  (Speedy répond en erreur Prisma).

## En cours — module 9 « Facturation » sur `feat/billing`

60 routes (dont les 6 reportées du module 8) ; 457/494 cochées. 56 tests unitaires, 115 de contrat.
- Relevés livreur PAI- : courses confirmées de la période (montant figé, sinon négocié, sinon grille),
  commissions déjà gardées aux remises soustraites, validation (une course payée une seule fois),
  règlement espèces / virement / chèque, photo du chèque, PDF. Côté livreur : `courier/me/cheques`.
  Branché : l'annulation d'une livraison recalcule les relevés brouillons qui portaient la course.
- Bons de versement BV- : COD des vendeurs professionnels (part chèque déduite), création en masse
  sous verrou en base par vendeur, versement une seule fois, PDF (pied réglable).
- Factures FA- (clients et clients Pro) : facturables, brouillon unique par vendeur, facture du mois,
  masse, ajustements, émission (TVA figée, colis « facturés »), PDF (identité et logo Pro), export
  deux feuilles, journées d'émission, PDF du jour ; règlements (solde, retour à « émise ») ; ordres
  de virement OV- (sélection ou factures, un colis viré une seule fois), PDF et classeur.
- `InvoiceDraftReplay` complété : un chèque rejeté ou annulé recalcule les lignes brouillons.
- Bug Speedy corrigé (argent) : la facturation EN MASSE ajoutait à une facture de régime normal un colis
  dont le COD était déjà versé par bon de versement (COD dû deux fois) ; il en est exclu, comme dans
  la liste des candidats et l'ajout manuel. Test `Bulk_invoice_skips_a_cod_already_paid_out_for_a_normal_client`.
- Reproduit : `unpaidOnly=false` vaut « vrai » (`z.coerce.boolean`), frontière de mois en UTC.

## Prochaine action exacte

1. Ouvrir la PR du module 9 (`feat/billing`), attendre la CI, fusionner, puis module 10 (réclamations,
   annonces, notifications, tableaux de bord : 37 routes).

## Blocages / décisions pour Salah

- Aucun bloquant. `LUCKYPENNY_LICENSE_KEY` absente de la session courante : avertissement de licence.
- Dépendance ajoutée hors liste : `Svg.Skia` (rendu des logos SVG, extension de SkiaSharp).

## Reprendre

1. `pg_ctl -D C:\Users\dell\pgdata-wimo -o "-p 5434" start` (ou `docker compose up -d postgres`).
2. `cd api && dotnet build Delivery.slnx`.
3. `TEST_DATABASE_URL="Host=localhost;Port=5434;Username=postgres;Database=postgres" dotnet test tests/Delivery.ContractTests`.
4. Lire « Prochaine action exacte ».
