# Déploiement — Wimo-Delivery (.NET)

## État de la production

| | |
| --- | --- |
| Mise en ligne | 2026-11-24, bascule à 17h04 (environ 20 s d'interruption) ; redéploiement `240c4fd` à 17h42 |
| Domaines | https://new.wimodelivery.ma (web), https://api-new.wimodelivery.ma (API) ; nginx inchangé |
| Commit déployé | `240c4fd` (`main`), checkout détaché dans `/opt/wimo-delivery/app-dotnet` ; précédent : `127c914` |
| Images | `wimo-delivery-dotnet-api:240c4fd`, `wimo-delivery-dotnet-web:240c4fd` (construites sur le VPS) ; celles de `127c914` sont gardées pour le rollback |
| Pile Docker | projet `wimo-delivery-dotnet` : `deploy/docker-compose.prod.yml` + `/opt/wimo-delivery/app-dotnet.env` (600, hors Git) |
| Ports | web `127.0.0.1:3102`, api `127.0.0.1:3103` (recette avant bascule : 3112 / 3113) |
| Base | `wimo_dotnet` (conteneur `wimo-delivery-dotnet-postgres-1`, volume `wimo-delivery-dotnet-postgres-data`), **nouvelle base** ; pièces jointes : volume `wimo-delivery-dotnet-uploads-data` |
| Migrations EF | `20261123223924_InitialSchema`, `20261124111629_RemittanceOneOpenPerCourier` |
| Recette | `scripts/recette.sh --cleanup` PASS **66/66** sur les URL publiques (`240c4fd`) ; tout ce qu'elle a créé est supprimé en base, compteurs restaurés |
| Base | **propre pour les tests** : aucun colis, facture, réclamation, vendeur ni livreur ; compteurs de numérotation à zéro ; seul compte : `wimo@gmail.com` ; aucun compte de démo |
| Sauvegarde | quotidienne 03h30 (root, `/etc/cron.d/wimo-delivery-backup` → `/opt/wimo-delivery/backup.sh` → `app-dotnet/deploy/backup.sh`), dumps `backups/wimo_dotnet_*.dump` vérifiés par `pg_restore -l`, rotation 14 jours |
| Ancienne pile NestJS | **arrêtée** (`docker compose stop`), conteneurs, volumes et images conservés ; base intacte |
| Dumps | avant bascule : `backups/wimo_avant-dotnet_20261124_163926.dump` (572 Ko, sha256 `32fd65a4…833f000e8b`) ; avant nettoyage : `backups/wimo_dotnet_avant-nettoyage_20261124_172239.dump` ; avant `240c4fd` : `backups/wimo_dotnet_avant-240c4fd_20261124_174200.dump` |

### Données reprises

Export : `deploy/export-referential.sh`. CSV dans `/opt/wimo-delivery/.runtime/export-referentiel/` (700, hors Git).

Import :
1. `import-referential` (image API) pour le référentiel ;
2. `deploy/import-company.sh` pour la société, ses paramètres, le compte et les tarifs de zone.

| Repris | Nombre |
| --- | --- |
| Zones | 5 |
| Villes (nom arabe compris, 199 sans nom arabe) | 396 |
| Quartiers | 9 540 |
| Agences | 1 (Wimo Casablanca) |
| Règles tarifaires (trois modes, départ CAS) | 396 |
| Tarifs de zone à zone | 5 |
| Paramètres société (étiquettes, thèmes, finance, tarifs…) | 28 |
| Comptes | `wimo@gmail.com` (super-admin, empreinte argon2id identique à l'ancienne base) |
| Banques (reprises de l'ancienne base) | 12 |
| Types de réclamation (repris de l'ancienne base) | 6 |

- **Statuts** : identiques à l'ancienne base (35).
- **Transitions** : 158 au lieu de 140. Les 18 de plus sont les transitions `RETURN_RECEPTION` (→ « récupéré par le livreur ») du flux de retours, absentes de l'ancienne version.
- **Société** : « Wimo Delivery », logo `/brands/wimo.png` (ajouté au web), coordonnées de production.
- **Palette** : « Bleu Wimo » `#0F62FE` / `#0A1F44`. La production affichait l'ambre `#C98A1B`.

**Non repris** (intacts dans le dump d'avant bascule) :
- les données de l'ancienne base : 7 colis, 2 vendeurs, 4 livreurs, 5 autres comptes, 1 facture, 5 circuits.

**Données de recette** : celles des deux premiers passages (colis, factures FA-2611-0001/0002, réclamations RCL-2611-0001/0002, comptes et super-admin de recette) ont été supprimées le 2026-11-24, et les compteurs remis à zéro. Depuis `240c4fd`, `recette.sh --cleanup` (par défaut) efface lui-même ce qu'il crée.

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

Revenir seulement au commit .NET précédent (`127c914`, images gardées) :

```bash
cd /opt/wimo-delivery/app-dotnet && git checkout -q --detach 127c914b4ceefc15abd2344f4116c0b83acf77a2
sed -i 's/^APP_VERSION=.*/APP_VERSION=127c914/' ../app-dotnet.env
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
