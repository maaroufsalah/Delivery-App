# Chantier Delivery-App : API .NET 10 compatible Speedy (rapport final)

Client : Wimo. Référence : Speedy (`delivery-platform`, NestJS), lue sans modification.
Le web (`frontend/`) et le mobile (`mobile/`) sont copiés de Speedy (`main` e9a98b06) **sans
modification de code** et parlent à l'API .NET.

## État à la livraison

| Contrôle | Résultat |
| --- | --- |
| Inventaire du contrat (`docs/contract/INVENTAIRE.md`) | **494 / 494** routes servies (506 dans Speedy, 12 hors périmètre `/platform` mono-société) |
| Tests unitaires | 66 verts |
| Tests de contrat (WebApplicationFactory + PostgreSQL) | 127 verts, dont un balayage de portée par module |
| CI GitHub Actions | verte sur chaque PR (build, unitaires, contrat, migrations à jour, recette) |
| `scripts/recette.sh` sur la pile docker compose (api + web Speedy + postgres) | **PASS, 55 / 55** |

## Modules livrés (une PR fusionnée par module)

| # | Module | Contenu |
| --- | --- | --- |
| 1 | Socle | Solution Clean Architecture, 104 entités EF (schéma Prisma sans `tenantId`), migration initiale, erreurs au format Nest, JSON `.fffZ`, inventaire, CI |
| 2 | Identité et accès | Auth, sessions et rotation des jetons, cookies `dp.at` / `dp.rt`, comptes, rôles, matrice des permissions, écarts par utilisateur, inscription publique, frein par IP |
| 3 | Société et référentiel | Paramètres, logo, villes, zones, quartiers, agences, types de livraison, tarifs, statuts, import CSV, seed Casablanca |
| 4 | Clients et livreurs | Fiches, client Pro, remises, villes desservies et tarifs livreur, comptes |
| 5 | Colis | CRUD, statuts, historique, corrections, destination, échanges, étiquettes PDF, import Excel, entrepôt, jetons prestataires, journal d'audit |
| 6 | Bons et circuits | BR- / BD-, circuits ramassage / livraison / retour, scan, validations partielles, clôtures, transferts, masse, espace livreur, stock livreur |
| 7 | Retours et BL | RL-, BRT-, BRB-, BL des clients Pro, PDF comparés à Speedy |
| 8 | Caisse et chèques | RF- (un seul ouvert par livreur), chèques (cycle complet, bordereaux), petite caisse (clôture à minuit), tableau de bord finance |
| 9 | Facturation | Relevés livreur PAI-, bons de versement BV-, factures FA- (normal et Pro, TVA), règlements, ordres de virement OV-, PDF et classeurs |
| 10 | Engagement | Notifications et push Expo, actualités, réclamations RCL-, tableaux de bord de pilotage (11 vues, export xlsx / csv) |
| 11 | Recette | `scripts/recette.sh`, service `web` dans docker compose, job CI `recette` |

## Recette (`scripts/recette.sh`)

Parcours joués par l'API publique uniquement, sans raccourci en base :

- **Admin** : crée la fiche et le compte d'un vendeur et d'un livreur, puis la grille du livreur.
- **Vendeur** : crée un colis (COD 250 MAD) et demande son ramassage.
- **Admin** : valide le bon (colis prêt), crée le circuit, y place le colis et le fait partir. L'étiquette PDF est contrôlée.
- **Livreur** : voit sa tournée et la notification d'affectation, livre le colis, et retrouve 250 MAD dans sa caisse.
- **Admin** : confirme la caisse et émet la facture (PDF contrôlé). Le tableau de bord du jour compte la livraison.
- **Vendeur** : lit sa facture et sa notification, puis ouvre une réclamation que l'agence résout.
- **Portée** : le livreur ne voit aucune facture ; le vendeur n'ouvre pas le tableau de bord.
- **Web Speedy** : la page de connexion et l'accueil public sont rendus. Avec la session posée par l'API, les pages admin `/fr/dashboard`, vendeur `/fr/parcels` et livreur `/fr/me` sont rendues aussi.

Lancement :

```bash
docker compose up -d --build
API=http://localhost:5080/api WEB=http://localhost:3000 ADMIN_EMAIL=admin@wimo.ma ADMIN_PASSWORD=<SEED_ADMIN_PASSWORD> scripts/recette.sh
```

Docker est absent du poste de développement. La partie API est donc jouée en local contre
`dotnet run` sur une base neuve (49/49), et la pile complète en CI (55/55).

## Bugs Speedy corrigés (argent toujours ; portée et filtres des réclamations avant mise en ligne)

Chacun est couvert par un test. **À corriger aussi dans Speedy.**

1. `POST /exchanges` passait le montant en centimes comme un COD en dirhams (COD × 100). Le COD vaut désormais `amount / 100` MAD.
2. Le « collecté » du livreur (performance, stock) ignorait le « déjà payé ». Il suit désormais `codToCollect`.
3. La facturation **en masse** ajoutait à une facture de régime normal un colis dont le COD était déjà versé par bon de versement : le COD était dû deux fois. Ce colis est maintenant exclu, comme dans la liste des candidats et l'ajout manuel.
4. **Réclamations, portée d'agence** (corrigé avant la mise en ligne, à la demande de Salah) : Speedy ouvrait à un agent les réclamations de toute la société. Un agent ne voit et ne traite plus que celles de ses agences (agence du colis, sinon celle du vendeur) ; hors portée, 404. Sans agence, il n'en voit aucune ; ouvrir une réclamation hors de son agence répond 403. L'admin garde tout. La notification « réclamation ouverte » part aux agents de l'agence concernée et aux super-admins.
5. **Réclamations, `late=false`** (corrigé avant la mise en ligne) : `z.coerce.boolean` en faisait un « vrai ». `late=false` (ou `0`) garde désormais les réclamations qui ne sont pas en retard.
6. Hors argent, mais corrigé à la demande de Salah : la photo de preuve du dernier colis d'un circuit tombait en 404, car le circuit se clôt automatiquement. Elle s'attache désormais au colis du livreur.

## Comportements Speedy reproduits (sans impact financier)

- `GET /courier/me/routes?status=closed` renvoie `[]`.
- À la remise d'un bon BRT-, seuls le nom et le type de la pièce jointe sont gardés, pas son contenu.
- Un statut de chèque inconnu dans le filtre ne sélectionne rien. Speedy, lui, répond par une erreur Prisma.
- `unpaidOnly=false` vaut « vrai » (`z.coerce.boolean`). Le même défaut sur `late=false` des réclamations est corrigé, voir plus haut.
- Frontière de mois en UTC (factures du mois).
- Réclamations : les statuts ne sont pas traduits dans l'export.
- Tableaux de bord :
  - `clients/:id` lit le nom du vendeur sans contrôle de portée ;
  - un agent sans agence voit les règlements, OV, écarts et soldes livreurs de toute la société ;
  - les alertes « en retard » sont calculées en heures depuis maintenant (UTC) ;
  - le nom du fichier CSV porte la date UTC ;
  - `delays.within48` est la tranche 24–48 h ;
  - l'écart de caisse est une somme nette ;
  - un refus (statut 6) compte comme échec, pas comme retour.
- PDF : page A4 de marque carrée et différences de `flex-shrink` acceptées (le contenu est identique).
- Un livreur ne revoit pas l'affectation d'un circuit clos dans son centre de notifications.

## Écarts assumés, choix d'infrastructure

- **Verrous en base, jamais en mémoire :**
  - RF- unique par livreur (verrou consultatif de transaction et index partiel, test de concurrence) ;
  - idempotence par verrou consultatif de session ;
  - débit des prestataires compté en base (`provider_calls`).
- **Annulation de livraison** (correction de statut) : argent, commission et ligne RF- sont annulés dans la même transaction, avec journal d'audit. Les relevés brouillons sont recalculés.
- **Push Expo hors requête.** Speedy pousse pendant la requête. Ici, chaque notification écrite est mise en file par un intercepteur EF, et un worker l'envoie :
  - lots de 100, 10 s par lot, texte dans la langue de l'appareil (catalogue fr/ar/en de Speedy) ;
  - les jetons `DeviceNotRegistered` sont supprimés ;
  - une transaction annulée n'envoie rien.
- **Workers inactifs en environnement « Testing »** : push et balayage de la petite caisse.
- **Dates du jour** : toujours `IClock`, fuseau Africa/Casablanca. Sous Windows, la base tz est fausse pour le Maroc d'octobre 2026 à mars 2027 ; en production Linux (tzdata), le calcul est correct.
- **Masque livreur** : appliqué au JSON, aux exports et aux PDF lus par un vendeur.
- **Tableaux de bord** : les requêtes partent en série (un seul DbContext), là où Speedy utilise `Promise.all`. Une date `from` / `to` hors calendrier (2026-02-30) est recalée comme `Date.UTC`, mais renvoyée corrigée.
- **Image web** : `next start` plutôt que la sortie autonome, car `next.config.ts` trace depuis l'arborescence de Speedy et le code du front n'est pas modifié. Seuls le web et `@delivery/shared` sont installés.
- **Dépendance ajoutée** : `Svg.Skia`, pour rendre les logos SVG.

## Ce qui reste pour le déploiement VPS (non fait ici, hors périmètre du chantier)

1. **Secrets de production** :
   - `JWT_ACCESS_SECRET`, `JWT_REFRESH_SECRET`, `SEED_ADMIN_PASSWORD` ;
   - `LUCKYPENNY_LICENSE_KEY` (licence MediatR / AutoMapper : sans elle, seul un avertissement s'affiche) ;
   - mot de passe Postgres.
2. **Domaine et web** : `NEXT_PUBLIC_API_URL` et `NEXT_PUBLIC_PLATFORM_DOMAIN` au build du web, `WEB_ORIGIN` et le domaine des cookies côté API.
3. **Reverse proxy** nginx / TLS dans `/opt/<projet>`. Diff à montrer à Salah avant toute modification de `/etc/nginx`.
4. **Exploitation** : volume `uploads` persistant et sauvegardes Postgres.
5. **Mobile** : build Expo pointé sur l'URL de l'API de production. Le push sort vers `exp.host` : il faut une sortie réseau ouverte.
6. **Données** : import du référentiel réel de Wimo (`dotnet run -- import <fichier>`) si un export est fourni. Le seed actuel est minimal (Casablanca).
7. **PDF finance** (relevés, bons de versement, factures, OV) : contenu repris de Speedy, comparaison visuelle page à page encore à faire. Les PDF de retours sont déjà comparés.
