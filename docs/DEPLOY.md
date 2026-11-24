# Déploiement — Wimo-Delivery (.NET)

## État de la production

| | |
| --- | --- |
| Mise en ligne | 2026-11-24, bascule à 17h04 (environ 20 s d'interruption) |
| Domaines | https://new.wimodelivery.ma (web), https://api-new.wimodelivery.ma (API) ; nginx inchangé |
| Commit déployé | `127c914` (`main`), checkout détaché dans `/opt/wimo-delivery/app-dotnet` |
| Images | `wimo-delivery-dotnet-api:127c914`, `wimo-delivery-dotnet-web:127c914` (construites sur le VPS) |
| Pile Docker | projet `wimo-delivery-dotnet` : `deploy/docker-compose.prod.yml` + `/opt/wimo-delivery/app-dotnet.env` (600, hors Git) |
| Ports | web `127.0.0.1:3102`, api `127.0.0.1:3103` (recette avant bascule : 3112 / 3113) |
| Base | `wimo_dotnet` (conteneur `wimo-delivery-dotnet-postgres-1`, volume `wimo-delivery-dotnet-postgres-data`), **nouvelle base** ; pièces jointes : volume `wimo-delivery-dotnet-uploads-data` |
| Migrations EF | `20261123223924_InitialSchema`, `20261124111629_RemittanceOneOpenPerCourier` |
| Recette | `scripts/recette.sh` PASS **64/64** sur 127.0.0.1:3112/3113, puis PASS **64/64** sur les URL publiques |
| Sauvegarde | quotidienne 03h30 (root, `/etc/cron.d/wimo-delivery-backup` → `/opt/wimo-delivery/backup.sh` → `app-dotnet/deploy/backup.sh`), dumps `backups/wimo_dotnet_*.dump` vérifiés par `pg_restore -l`, rotation 14 jours |
| Ancienne pile NestJS | **arrêtée** (`docker compose stop`), conteneurs, volumes et images conservés ; base intacte |
| Dump d'avant bascule | `/opt/wimo-delivery/backups/wimo_avant-dotnet_20261124_163926.dump` (572 Ko, sha256 `32fd65a4…833f000e8b`) |

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

- **Statuts** : identiques à l'ancienne base (35).
- **Transitions** : 158 au lieu de 140. Les 18 de plus sont les transitions `RETURN_RECEPTION` (→ « récupéré par le livreur ») du flux de retours, absentes de l'ancienne version.
- **Société** : « Wimo Delivery », logo `/brands/wimo.png` (ajouté au web), coordonnées de production.
- **Palette** : « Bleu Wimo » `#0F62FE` / `#0A1F44`. La production affichait l'ambre `#C98A1B`.

**Non repris** (données de l'ancienne base, intactes dans le dump) :
- 7 colis ;
- 2 vendeurs et 4 livreurs ;
- 5 autres comptes ;
- 1 facture et 5 circuits ;
- les 12 banques et les 6 types de réclamation.

**Traces de recette dans la base de production.** Deux passages, sur 3112 puis sur les URL publiques, ont laissé :
- les vendeurs « Boutique r1795539796 » et « Boutique r1795539873 », désactivés, avec leurs livreurs « Karim Recette », désactivés ;
- les colis `CAS240012611CAS` et `CAS240022611CAS` ;
- les factures `FA-2611-0001` et `FA-2611-0002` ;
- les réclamations `RCL-2611-0001` et `RCL-2611-0002`.

Les comptes de recette sont désactivés et supprimés, comme le super-admin temporaire `recette-admin@wimodelivery.ma`. Les numéros FA-2611-0001/0002 sont consommés.

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

Elle demande un super-admin.
1. Mettre `SEED_ADMIN_EMAIL` (nouvelle adresse) et `SEED_ADMIN_PASSWORD` dans `app-dotnet.env`, puis `up -d api` (le seed le crée).
2. Lancer sur le VPS : `API=https://api-new.wimodelivery.ma/api WEB=https://new.wimodelivery.ma ADMIN_EMAIL=… ADMIN_PASSWORD=… bash app-dotnet/scripts/recette.sh`.
3. Désactiver ensuite ce compte et vider `SEED_ADMIN_PASSWORD`.

Chaque passage laisse un colis, une facture et une réclamation de recette.

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
5. **Données de recette** : les supprimer de la base de production si Wimo le souhaite (liste ci-dessus).
6. **Référentiel à compléter** : recréer les banques (12) et les types de réclamation (6) dans l'interface s'ils servent.
