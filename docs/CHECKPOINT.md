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

## En cours — module 3 « Société et référentiel », PR ouverte depuis `feat/referential`

68/68 routes : villes, zones (+ tarifs inter-zones), quartiers, agences (siège), types de
livraison, `pricing-rules` (grille par origine, import/export xlsx), `directory/*`,
`settings/*` (société, business, finance, logo SkiaSharp, thème + instantanés),
`public/company|logo|rates|mobile-config|cities`. Seed : statuts + transitions
(`scripts/ts/dump-statuses.mjs`), paramètres Wimo, « Livraison normale », zone/ville/agence
Casablanca. Import CSV idempotent : `scripts/import-referential.sh <dossier>` (format dans
`ReferentialImporter.cs`). Mobile : `PRODUCTION_API_ORIGIN` lu dans `EXPO_PUBLIC_API_URL`
(eas.json production = `https://api-new.wimodelivery.ma`).

## Prochaine action exacte

1. Fusionner la PR du module 3 une fois la CI verte.
2. Module 4 « Clients et livreurs » sur `feat/accounts` : lire `modules/clients`, `modules/couriers`,
   `modules/merchant` de Speedy (inventaire section Module 4, 39 routes) ; y porter aussi la
   validation/le refus des inscriptions et le masque livreur (`common/scope/courier-mask.ts`).

## Blocages / décisions pour Salah

- Aucun bloquant. `LUCKYPENNY_LICENSE_KEY` absente de la session courante : avertissement de licence.
- Dépendance ajoutée hors liste : `Svg.Skia` (rendu des logos SVG, extension de SkiaSharp).

## Reprendre

1. `pg_ctl -D C:\Users\dell\pgdata-wimo -o "-p 5434" start` (ou `docker compose up -d postgres`).
2. `cd api && dotnet build Delivery.slnx`.
3. `TEST_DATABASE_URL="Host=localhost;Port=5434;Username=postgres;Database=postgres" dotnet test tests/Delivery.ContractTests`.
4. Lire « Prochaine action exacte ».
