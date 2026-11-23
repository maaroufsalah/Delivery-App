# Delivery-App (Wimo)

API .NET 10 réécrite à l'identique du contrat NestJS de Speedy ; `frontend/` (Next.js) et
`mobile/` (Expo) sont copiés de Speedy **sans modification de code**.

Référence (LECTURE SEULE) : `D:\Work\Projects\Freelance\Anass\delivery-platform`
(`apps/api/src` = règles métier, `packages/shared` = schémas zod et types du contrat).
Checklist du chantier : `docs/contract/INVENTAIRE.md`. Reprise : `docs/CHECKPOINT.md`.

## Règles

- Contrat d'abord : mêmes routes, verbes, query, JSON camelCase, formes de réponse, dates
  ISO à la milliseconde, décimaux comme `JSON.stringify`, codes HTTP et corps d'erreur
  `{ code, message, details? }`. Comportement douteux de Speedy : le reproduire, le noter.
- Clean Architecture : Domain / Application / Infrastructure / Api.
- CQRS MediatR : un fichier par cas d'usage (`Application/<Module>/Commands|Queries/X.cs`
  = requête + validator + handler). DTO partagés dans `Application/<Module>/Dtos/`.
- Handlers → `IAppDbContext` directement. Logique réutilisée → méthode d'entité du Domain.
- Contrôleurs minces : uniquement `ISender.Send`.
- Behaviors : Validation (422 format Nest), Logging, Performance (> 500 ms). Rien d'autre.
- AutoMapper : un Profile par module, `ProjectTo` en lecture, `Map` en écriture.
- INTERDIT : repository, UnitOfWork, couche Service doublon, abstraction sans besoin.
  Interfaces seulement pour l'infra : ICurrentUser, IJwtService, IPdfService, IExcelService,
  IFileStorage, IClock.
- Portée côté serveur : vendeur = ses données, livreur = les siennes, agent = son agence.
  Un test de balayage de portée par module.
- Mono-société : pas de tenantId (retiré du schéma Prisma au portage), pas de /platform.
- Nullable, TreatWarningsAsErrors, CancellationToken partout, AsNoTracking en lecture.
- Tests : xUnit + NSubstitute + Shouldly. Contrat : WebApplicationFactory + PostgreSQL, en série.
- PC 7,6 Go : jamais deux builds/tests en parallèle. Pas de `pnpm install` du mobile.

## Git

Branche `feat/<module>` → commits Conventional Commits, fichiers ajoutés nommément
(jamais `git add .`), `git diff --cached --stat` avant chaque commit → PR → CI verte →
`gh pr merge --squash --delete-branch`. Aucune mention d'outil d'assistance dans les
commits, PR, branches ou le code.

## Commandes (depuis `api/`)

```bash
dotnet build Delivery.slnx
dotnet test tests/Delivery.UnitTests
# Contrat : Postgres local (sinon Testcontainers/Docker)
TEST_DATABASE_URL="Host=localhost;Port=5434;Username=postgres;Database=postgres" dotnet test tests/Delivery.ContractTests
dotnet test tests/Delivery.ContractTests --filter "FullyQualifiedName~Identity"
dotnet ef migrations add <Nom> -p src/Delivery.Infrastructure -s src/Delivery.Api -o Persistence/Migrations
dotnet run --project src/Delivery.Api    # http://localhost:5080/api
node ../scripts/gen-inventory.mjs <speedy>/apps/api/src   # regénère l'inventaire (perd les coches)
```

Postgres local de dev : `pg_ctl -D C:\Users\dell\pgdata-wimo -o "-p 5434" start`
(base `wimo_delivery`, rôle `wimo`/`wimo`), ou `docker compose up -d postgres`.
