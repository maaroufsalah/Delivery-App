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

## En cours — module 5 « Colis » sur `feat/parcels` (non poussée)

Fait et commité : liste, export, stats, fiche, next-code, quote, création (idempotence), changement
de statut (gardes terminal/contestation/argent/circuit), fees, expected-payment, bl, notes, PATCH/DELETE
vendeur, admin-delete (échange annulé avec), client (+preview, empreinte). 7 tests de contrat verts.
`Notifications/Notifier.cs` : lignes en base + préférences ; l'envoi push viendra au module 10.

**Reste au module 5** : destination ; exchange + `/exchanges` ; import Excel ; étiquettes PDF +
label-jobs ; bulk-jobs ; audit ; entrepôt (products, stock, storage-requests, orders) ; provider tokens
+ API ; courier/me ; public/track ; merchant/me/summary ; masque livreur.

**Dettes à solder au module 6** : statut 13 « En cours de livraison » lève 500 « Mise en circuit
indisponible » (comme Speedy sans `OutForDeliveryPlacer`) — brancher la mise en circuit.

## Prochaine action exacte

1. `PATCH /parcels/:code/destination` (`parcels.service.ts` l. 2095-2471), puis la suite de la liste.

## Blocages / décisions pour Salah

- Aucun bloquant. `LUCKYPENNY_LICENSE_KEY` absente de la session courante : avertissement de licence.
- Dépendance ajoutée hors liste : `Svg.Skia` (rendu des logos SVG, extension de SkiaSharp).

## Reprendre

1. `pg_ctl -D C:\Users\dell\pgdata-wimo -o "-p 5434" start` (ou `docker compose up -d postgres`).
2. `cd api && dotnet build Delivery.slnx`.
3. `TEST_DATABASE_URL="Host=localhost;Port=5434;Username=postgres;Database=postgres" dotnet test tests/Delivery.ContractTests`.
4. Lire « Prochaine action exacte ».
