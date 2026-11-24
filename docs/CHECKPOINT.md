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

## En cours — module 4 « Clients et livreurs », PR ouverte depuis `feat/accounts`

35 routes : `/clients` (fiche, patch partiel, TVA Pro, frais de ramassage, validation/refus des
inscriptions, compte, remises sans doublon, adresses, documents), `/couriers` (fiche, grille de paie
par ville auditée, validation/refus, compte), `/merchant/me/company`. Rangées ailleurs :
`/merchant/me/summary` → module 5, `/merchant/me/pickups|dropoffs|notes` → module 6.

## Prochaine action exacte

1. Fusionner la PR du module 4 une fois la CI verte.
2. Module 5 « Colis » sur `feat/parcels` (inventaire section Module 5) : commencer par
   `modules/parcels/parcels.service.ts` (création, code colis, tarification), puis statuts/historique,
   journal d'audit générique (`common/audit/audit.extension.ts`), masque livreur pour les vendeurs
   (`common/scope/courier-mask.ts`), échanges, import Excel, étiquettes PDF.

## Blocages / décisions pour Salah

- Aucun bloquant. `LUCKYPENNY_LICENSE_KEY` absente de la session courante : avertissement de licence.
- Dépendance ajoutée hors liste : `Svg.Skia` (rendu des logos SVG, extension de SkiaSharp).

## Reprendre

1. `pg_ctl -D C:\Users\dell\pgdata-wimo -o "-p 5434" start` (ou `docker compose up -d postgres`).
2. `cd api && dotnet build Delivery.slnx`.
3. `TEST_DATABASE_URL="Host=localhost;Port=5434;Username=postgres;Database=postgres" dotnet test tests/Delivery.ContractTests`.
4. Lire « Prochaine action exacte ».
