# CHECKPOINT — chantier « Delivery-App » (API .NET pour Wimo)

Rapport final : `docs/chantiers/delivery-app-dotnet.md` (en fin de chantier). Checklist : `docs/contract/INVENTAIRE.md`.

## Fait

- PR 1 (socle) : web/mobile/packages copiés de Speedy `main` e9a98b06 (seuls changent les
  `.env.example`, l'hôte API par défaut `localhost:5080` et `PRODUCTION_API_ORIGIN` du mobile),
  inventaire de 506 endpoints (494 dans le périmètre, 12 /platform hors périmètre), solution
  .NET 10 (4 projets + 2 projets de tests), schéma Prisma porté en 104 entités EF Core
  (`scripts/prisma-to-ef.py`, `companyId` retiré), migration `InitialSchema`, middleware
  d'erreurs au format Nest, sérialisation JSON (dates `.fffZ`, décimaux JS), CI GitHub Actions.

## En cours

- (rien)

## Prochaine action

- Module 2 « Identité et accès » sur `feat/identity`.

## Blocages / décisions pour Salah

1. `LUCKYPENNY_LICENSE_KEY` absente du poste (ni User ni Machine) et des secrets GitHub du
   dépôt : MediatR/AutoMapper tournent mais journalisent un avertissement de licence.
2. Docker absent du poste : tests de contrat joués en local sur Postgres (scoop, port 5434,
   `TEST_DATABASE_URL`) ; Testcontainers ne tourne qu'en CI.
3. Hôte API de production du mobile : `https://api.wimo.ma` posé en attente du vrai nom.

## Reprendre

1. `pg_ctl -D C:\Users\dell\pgdata-wimo -o "-p 5434" start` (ou `docker compose up -d postgres`).
2. `cd api && dotnet build Delivery.slnx`.
3. Lire la section « Prochaine action », ouvrir la section du module dans l'inventaire.
