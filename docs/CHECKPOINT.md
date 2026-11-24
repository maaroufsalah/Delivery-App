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

## En cours — module 6 « Bons et circuits » sur `feat/routes` (non poussée)

Lots : (A) bons BR-/BD- + demandes vendeur + tâches de masse — FAIT (commit `feat(notes)…`) ;
(B) circuits : liste, stats, export, fiche, création, modif, suppression, candidats, ajout/retrait de
colis et de bons, ramassage d'un bon ; (C) départ, confirmation des issues (encaissement, commission,
retour de fonds, chèque), clôture ; (D) statut 13 (`OutForDeliveryPlacer`, test de contrat qui prouve
la fin du 500), correction de statut, annulation de livraison, `field-status`, transfert ;
(E) `courier/*`, `courier-stock`, statut prestataire, `staff/me/alerts` ; (F) PDF fidèles (étiquettes,
reçu d'entreposage, feuille de route, bon) comparés dans `docs/design/labels/`.
Reste à faire au lot (A) : PDF du bon (`GET /delivery-notes/:code/pdf`, lot F) ; découper
`NoteUseCases.cs` un fichier par cas d'usage.
Décisions : frais de ramassage écrits sur la facture brouillon (`Invoices/InvoiceDrafts.cs`, totaux
portés depuis `invoices.service.ts`) ; `GET /bulk-jobs` sans tâche = 200 corps vide (comme Nest) ;
identité des tâches de masse rejouée par `CurrentUser.Background`.

## Prochaine action exacte

1. Lot (B) : `routes.service.ts` l. 343-1019 (liste, fiche), 1076-1834 (écritures), 2420-2955 (bons du circuit).

## Blocages / décisions pour Salah

- Aucun bloquant. `LUCKYPENNY_LICENSE_KEY` absente de la session courante : avertissement de licence.
- Dépendance ajoutée hors liste : `Svg.Skia` (rendu des logos SVG, extension de SkiaSharp).

## Reprendre

1. `pg_ctl -D C:\Users\dell\pgdata-wimo -o "-p 5434" start` (ou `docker compose up -d postgres`).
2. `cd api && dotnet build Delivery.slnx`.
3. `TEST_DATABASE_URL="Host=localhost;Port=5434;Username=postgres;Database=postgres" dotnet test tests/Delivery.ContractTests`.
4. Lire « Prochaine action exacte ».
