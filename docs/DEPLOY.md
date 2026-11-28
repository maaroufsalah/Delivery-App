# Déploiement — Wimo-Delivery (.NET)

## État de la recette

new./api-new.wimodelivery.ma est la **recette (UAT) de Wimo** : données réelles reprises de Wimo Laravel le 2026-11-28.
La production viendra plus tard sur un autre domaine, avec le même script de reprise.

| | |
| --- | --- |
| Mise en ligne | 2026-11-24 (bascule depuis NestJS) ; `e692511` le 2026-11-25 ; `b80f0aa` le 2026-11-27 ; **`d762905` le 2026-11-28 à 01h20 UTC** |
| Domaines | https://new.wimodelivery.ma (web), https://api-new.wimodelivery.ma (API) ; nginx inchangé |
| Commit déployé | `d762905` (`main`, #24 : reprise complète de Wimo Laravel), checkout détaché dans `/opt/wimo-delivery/app-dotnet` ; précédent : `b80f0aa` |
| Images | `wimo-delivery-dotnet-api:d762905` (contient l'outil `LegacyImport.dll`), `wimo-delivery-dotnet-web:d762905` ; celles de `b80f0aa` (rollback) et `e692511` sont gardées |
| Pile Docker | projet `wimo-delivery-dotnet` : `deploy/docker-compose.prod.yml` + `/opt/wimo-delivery/app-dotnet.env` (600, hors Git) |
| Ports | web `127.0.0.1:3102`, api `127.0.0.1:3103` |
| Base | `wimo_dotnet` (conteneur `wimo-delivery-dotnet-postgres-1`, volume `wimo-delivery-dotnet-postgres-data`) ; pièces jointes : volume `wimo-delivery-dotnet-uploads-data` |
| Migrations EF | `InitialSchema`, `RemittanceOneOpenPerCourier`, `CarryOverNegativeInvoices`, **`20261127235229_LegacyImport`** (additive : `legacy_mappings`, `importedAt` sur factures, règlements, caisses, relevés, circuits, bons) |
| Recette | `scripts/recette.sh --cleanup` PASS **66/66** (`d762905`, 2026-11-28, après la reprise) ; `scripts/recette-reprise.sh` PASS **47/47** (comptes et données importés, voir ci-dessous) ; tout ce que les deux passages ont créé est supprimé, compteurs restaurés, empreintes d'origine des comptes importés remises, super-admin de recette supprimé, `SEED_ADMIN_*` vidés |
| Sauvegarde | quotidienne 03h30 (root, `/etc/cron.d/wimo-delivery-backup` → `/opt/wimo-delivery/backup.sh` → `app-dotnet/deploy/backup.sh`), dumps `backups/wimo_dotnet_*.dump` vérifiés par `pg_restore -l`, rotation 14 jours |
| Ancienne pile NestJS | **arrêtée** (`docker compose stop`), conteneurs, volumes et images conservés ; base intacte |
| Dumps | avant `d762905` : `backups/wimo_dotnet_avant-d762905_20261128_011819.dump` ; **avant la reprise** : `backups/wimo_dotnet_avant-reprise-laravel_20261128_013530.dump` (1,2 Mo, sha256 `7c5467c925b71f52…`) ; plus anciens : avant bascule `wimo_avant-dotnet_20261124_163926.dump`, avant `e692511` `wimo_dotnet_avant-e692511_20261125_191631.dump` |
| Disque | 2026-11-28 : `docker builder prune -f` (deux fois, aucune image ni volume touché) ; 6 Go libres (92 %) après le déploiement — à surveiller avant le prochain build |

### Reprise Wimo Laravel (2026-11-28, 01h35 UTC)

Source : dump Laravel `wimo-laravel_20261128_013530.sql.gz` (mysqldump `--single-transaction`, lecture seule ; aucune
écriture ni aucun redémarrage de `/opt/wimo-wimo`). Données personnelles : uniquement sous
`/opt/wimo-delivery/.runtime/laravel-reprise/` (700) — dumps, CSV, rapports `rapport-dry-run.md` et `rapport-apply.md`,
relevés de contrôle. Correspondance des tables et des 38 statuts : `docs/migration/laravel-mapping.md`.

| Entité | Laravel (lues) | Importées | Rejetées (raison) |
| --- | ---: | ---: | --- |
| Colis | 2 193 | 2 029 | 164 supprimés dans Laravel |
| Historique des statuts | 10 274 | 9 969 | 305 de colis supprimés |
| Bons de ramassage et de dépôt | 492 | 488 | 4 de vendeurs supprimés |
| Circuits | 634 | 634 | — (48 gardés ouverts, colis encore en cours ; les autres clos) |
| Factures clients | 184 | 184 | — |
| Règlements | 181 | 176 | 5 de factures supprimées dans Laravel (ids 1 à 5) |
| Factures livreurs (relevés) | 1 | 1 | — |
| Recettes (caisses) | 276 | 276 | — (2 brouillons plus anciens d'un même livreur → « Annulé ») |
| Échanges ECH- | 10 | 7 | 3 dont le colis ou l'origine est supprimé |
| Comptes | 83 | 59 | 11 supprimés, 10 partenaires API, 2 e-mails en double, 1 vendeur sans fiche |
| Vendeurs / livreurs | 40 / 26 | 34 / 20 | supprimés ou sans compte |
| Villes / quartiers | 464 / 10 164 | 455 / 9 540 | 9 villes supprimées ; quartiers de villes supprimées, doublons fusionnés |
| Remises / grilles livreur / tarifs | 184 / 345 / 5 711 | 162 / 320 / 443 | sans prix, doublons, villes supprimées |
| Compteurs de colis (jour, départ, arrivée) | — | 1 257 | factures, circuits, bons, recettes : codes Laravel aléatoires, aucun compteur à repositionner |

- **Contrôles d'argent : zéro écart** (133 lignes) contre une copie MariaDB isolée du même dump : par client, nombre,
  COD, frais et net des factures, règlements, COD et frais des colis ; par livreur, encaissé, commissions et net des
  recettes, factures livreur ; colis par statut. Rejoués après les tests fonctionnels : toujours zéro écart.
- **Échantillons : 26/26 identiques** champ par champ (10 colis dont 2 échanges et 2 retours, 5 clients, 5 livreurs,
  3 factures, 3 circuits).
- **Statuts tranchés au plus proche** : 36 « 3eme appel + SMS » → 33 (4 colis) ; 15 → 16 et 30 → 7 (aucun colis).
- **Mots de passe** : empreintes bcrypt `$2y$` copiées telles quelles, vérifiées puis refaites en argon2id à la première
  connexion ; `wimo@gmail.com` reste l'administrateur principal (mot de passe inchangé).
- **Réglages repris de Laravel** : frais de ramassage et frais d'échec à 0 (comme Wimo Laravel) ; la validation d'un bon
  n'ouvre donc plus de facture brouillon avec des frais de ramassage.
- **Documents importés** : factures validées ou payées, règlements, caisses reçues, relevé livreur validé, circuits et
  bons clos portent `importedAt` et ne se modifient pas ; les brouillons Laravel restent vivants dans .NET.

**Relancer la reprise** (rafraîchir la recette, ou la future production) : sur le VPS, depuis `/opt/wimo-delivery`,

```bash
sh app-dotnet/deploy/reprise-laravel.sh               # dump frais, copie isolée, sauvegarde, essai, application, contrôles
sh app-dotnet/deploy/reprise-laravel.sh --dry-run-only # essai à blanc seul
```

Relançable sans doublon (`legacy_mappings`) : une relance ne réécrit que ce qui a changé côté Laravel. Un écart d'argent
restaure la sauvegarde prise juste avant et arrête le script. Puis rejouer la recette des données importées :

```bash
PSQL="docker exec -i wimo-delivery-dotnet-postgres-1 psql -U wimo -d wimo_dotnet" API=https://api-new.wimodelivery.ma/api   ADMIN_EMAIL=… ADMIN_PASSWORD=… bash app-dotnet/scripts/recette-reprise.sh
```

### Rollback (exact)

```bash
ssh wimo-vps
cd /opt/wimo-delivery
docker compose -p wimo-delivery-dotnet -f app-dotnet/deploy/docker-compose.prod.yml --env-file app-dotnet.env stop api web
docker compose -f docker-compose.prod.yml start
cp -p backup-nestjs.sh backup.sh        # la sauvegarde de 03h30 repart sur la base NestJS
curl -s https://api-new.wimodelivery.ma/health
```

Les données écrites dans la pile .NET depuis la bascule ne remontent pas dans l'ancienne base.

Revenir seulement au commit .NET précédent (`240c4fd`, images gardées) :

```bash
cd /opt/wimo-delivery/app-dotnet && git checkout -q --detach 240c4fd
sed -i 's/^APP_VERSION=.*/APP_VERSION=240c4fd/' ../app-dotnet.env
docker compose -p wimo-delivery-dotnet -f deploy/docker-compose.prod.yml --env-file ../app-dotnet.env up -d --no-build api web
```

### Redéployer un nouveau commit

```bash
cd /opt/wimo-delivery/app-dotnet && git fetch -q origin main && git checkout -q --detach <sha>
sed -i 's/^APP_VERSION=.*/APP_VERSION=<sha court>/' ../app-dotnet.env
C="docker compose -p wimo-delivery-dotnet -f deploy/docker-compose.prod.yml --env-file ../app-dotnet.env"
$C build api && $C build web       # l'un après l'autre : mémoire du VPS
$C up -d --no-build                # migrations EF appliquées au démarrage de l'API
```

Le VPS ne commite ni ne pousse jamais.

### Rejouer la recette

Elle demande un super-admin temporaire.
1. Mettre `SEED_ADMIN_EMAIL=recette-admin@wimodelivery.ma` et un `SEED_ADMIN_PASSWORD` dans `app-dotnet.env`, puis `up -d api` (le seed crée le compte).
2. Lancer sur le VPS :
   ```bash
   CLEANUP_PSQL="docker exec -i wimo-delivery-dotnet-postgres-1 psql -U wimo -d wimo_dotnet"    API=https://api-new.wimodelivery.ma/api WEB=https://new.wimodelivery.ma ADMIN_EMAIL=… ADMIN_PASSWORD=…    bash app-dotnet/scripts/recette.sh --cleanup
   ```
   Tout ce que le passage crée est supprimé en base, et les compteurs reviennent à leur valeur d'avant si personne n'a numéroté pendant le passage.
3. Supprimer ensuite le super-admin de recette (`users`, `user_roles`, `refresh_sessions`, `audit_logs` de ce compte), puis vider `SEED_ADMIN_*` et `up -d api`.

## Ce qui reste

1. **APK Wimo** : reconstruire l'application mobile pointée sur `https://api-new.wimodelivery.ma/api`.
2. **Retrait définitif de la pile NestJS** après une semaine sans incident :
   - supprimer ses conteneurs, ses images et son volume de base, avec un dump conservé ;
   - supprimer `backup-nestjs.sh` ;
   - nettoyer la rotation des anciens `backups/wimo_*.dump`.
3. **Bascule future vers app.wimodelivery.ma** :
   - certificat et vhost nginx (diff à montrer avant toute modification de `/etc/nginx`) ;
   - `WEB_ORIGIN` et `PUBLIC_API_URL` ;
   - reconstruction de l'image web (`NEXT_PUBLIC_API_URL` est figé au build).
4. **`LUCKYPENNY_LICENSE_KEY`** (licence MediatR / AutoMapper) : absente de `app-dotnet.env`. Sans elle, l'API tourne mais journalise un avertissement de licence. À renseigner, puis `up -d api`.
5. **Lint web** : `frontend/scripts/check-messages.mjs` échoue sur 15 tirets cadratins des catalogues de traduction (hérités de Speedy, déjà présents avant `e692511`) — à corriger dans un chantier dédié.
