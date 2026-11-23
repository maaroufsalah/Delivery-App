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

## En cours — module 3 « Société et référentiel », branche `feat/referential` (commit wip poussé)

Écrit et compilé, **pas encore testé ni branché** sur un contrôleur :
- Domain : `Parcels/StatusCatalog(.Data).cs` (35 statuts + transitions, produits par
  `scripts/ts/dump-statuses.mjs`), `Pricing/DeliveryPricing.cs` (`quoteDeliveryFee`),
  `Common/Money.cs` (centimes, enums « wire »), `Common/Colors.cs` (HEX, contraste WCAG).
- Application : `Common/Security/References.cs` (assertDeletable, grille interne),
  `Cities/` complet (liste, export, détail, création, modification, actif, suppression).
- Outil : `scripts/split-usecases.py` (un fichier par cas d'usage ; refuse un préambule partagé).

Reste dans le module 3 (68 routes, lecture Speedy déjà faite) : contrôleur `/cities`, zones
(+ `zones/rates`), quartiers, agences, types de livraison, `pricing-rules` (dont `by-origin`,
import/export xlsx), `directory/*`, `settings/*` (société, business, finance, logo, thème),
`public/company|logo|rates|mobile-config|cities` ; seed statuts/transitions + type
« Livraison normale » + référentiel minimal Casablanca ; tests de contrat ; PR.

## Prochaine action exacte

1. `git checkout feat/referential && git pull`.
2. Écrire `api/src/Delivery.Api/Controllers/CitiesController.cs` (garde `NotMerchant`,
   `directory.read|write`, query `zoneId` multi-valeur), puis les tests de contrat des villes.
3. Enchaîner zones → quartiers → agences → types de livraison → tarifs → directory → settings → public.

## Blocages / décisions pour Salah

1. `LUCKYPENNY_LICENSE_KEY` absente du poste et des secrets GitHub : MediatR/AutoMapper tournent
   avec un avertissement de licence.
2. Docker absent du poste : tests de contrat locaux sur Postgres scoop (port 5434,
   `TEST_DATABASE_URL`) ; Testcontainers en CI seulement.
3. Hôte API de production du mobile : `https://api.wimo.ma` posé en attente du vrai nom.
4. Référentiel Wimo réel : seul le dump Laravel du VPS le contient (hors Git, données
   personnelles) → seed minimal Casablanca prévu, à remplacer par un export du référentiel.
5. Logo (module 3) : le redimensionnement/assainissement d'image de Speedy (sharp) demande une
   bibliothèque d'image hors liste autorisée (SkiaSharp envisagé) — à valider.

## Reprendre

1. `pg_ctl -D C:\Users\dell\pgdata-wimo -o "-p 5434" start` (ou `docker compose up -d postgres`).
2. `cd api && dotnet build Delivery.slnx`.
3. `TEST_DATABASE_URL="Host=localhost;Port=5434;Username=postgres;Database=postgres" dotnet test tests/Delivery.ContractTests`.
4. Lire « Prochaine action exacte ».
