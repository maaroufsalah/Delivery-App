# Reprise de Wimo Laravel (référentiel et comptes)

Source : dump MariaDB 10.4 de la production Wimo Laravel. Ni colis ni données financières (bons, circuits, boxes,
factures, règlements, remboursements, recettes, suivi, `partner_outbox`).

## Chaîne

```bash
# 1. Le dump en CSV, sans serveur MySQL (données réelles : dossier local, hors dépôt)
python -I scripts/laravel-dump-to-csv.py <dump.sql.gz> <dossier-csv>
# 2. À blanc (défaut) : transaction annulée, compte rendu par table (lus / importés / ignorés + raison)
dotnet run --project api/src/Delivery.Api -- import-laravel <dossier-csv>
# 3. Application, puis seed (statuts, permissions, société, réglages manquants)
dotnet run --project api/src/Delivery.Api -- import-laravel <dossier-csv> --apply
```

Idempotent (clés naturelles : nom de zone, code de ville, ville + quartier, nom d'agence, paire de villes, e-mail,
compte du vendeur / du livreur). Un rejeu n'écrase jamais le mot de passe d'un compte existant.

## Correspondance

| Laravel | .NET | Règle |
|---|---|---|
| `zones` | `Zone` | `tarif` → BaseRate, `tarif_ville` → SameCityRate (NULL → BaseRate) ; + zone « Hors zone » |
| `zone_tarifs` | `ZoneRate` | lignes avec un tarif |
| `villes` | `City` + `ZoneCity` | zone lue dans `zones.villes` ; aucune → « Hors zone » |
| `quartiers` | `District` | par ville, doublons de nom fusionnés |
| `tarifs` | `PricingRule` | quartier / agence / point relais ; NULL = 0 (calcul Laravel `+= null`) ; toujours active (Laravel ignore `enabled`) |
| `agences` | `Agency` | |
| `banques`, `compte_bancaires` | `Bank`, `BankAccount` | RIB sans espaces |
| `app_configs` 1 à 4 | réglages `pricing.*` | MAD → centimes |
| `colis_statuts` | catalogue .NET | mêmes identifiants (35/38), rien n'est écrit |
| `users` rôle 1 | `User` SUPER_ADMIN / STAFF | aucun ou les 17 droits `user_modules` → SUPER_ADMIN ; partiels → STAFF de son agence |
| `users` rôle 2 + `clients` | `User` MERCHANT + `Client` | ecommercant → MERCHANT, particulier → INDIVIDUAL |
| `client_remises` | `ClientDiscount` | `frais_livraison` → FlatRate, `meme_ville` (un PRIX) → SameCityRate, `frais_ramassage` → PickupFee |
| `client_ramassage_adresses` | `ClientPickupAddress` | adresses identiques fusionnées |
| `users` rôle 4 + `livreurs` | `User` COURIER + `Courier` | Moto / Voiture / Camionnette → MOTORBIKE / CAR / VAN, banque et RIB |
| `livreur_ville` | `CourierCity` | `frais_livraison` → DeliveryFee, `frais_ramassage` → PickupFee |
| mot de passe `$2y$` | `PasswordHash` tel quel | bcrypt vérifié, refait en argon2id à la première connexion |

## Décisions

1. Statuts 15 « En agence destination », 30 « Au guichet Amana », 36 « 3eme appel Pas de réponse + SMS » : sans
   équivalent .NET, ignorés et listés.
2. Comptes du personnel : aucun droit ou tous les droits → SUPER_ADMIN ; droits partiels → STAFF de son agence.
   wimo@gmail.com : rapproché par e-mail, mot de passe jamais écrasé (règle générale : un compte existant garde son
   mot de passe et son profil).
3. Comptes, clients, livreurs et villes supprimés : non importés, listés. Un vendeur (ou livreur) sans fiche active
   n'est pas importé (user 464).
4. Villes sans zone : `tarifs` est une grille ville → ville sans zone, Laravel ne connaît donc aucune « zone de
   destination » pour ces villes ; comme dans Laravel (aucune remise de zone ne s'y applique), elles vont dans
   « Hors zone ». Codes en double : la ville la plus ancienne garde le code, l'autre prend un code dérivé (lettres du
   nom), les deux sont listées.
5. Tarifs sans ville de départ ou sans aucun prix : ignorés. `client_remises` : Laravel n'applique que
   `enabled = true` (`ColisCreationService::calculeFraisLivraison`, `ColisController`) → NULL et 0 repris INACTIFS.
6. RIB des clients : pas de colonne .NET, listés au compte rendu. E-mails en double : le compte actif, puis le plus
   récemment modifié, est gardé.
7. `livreur_ville` en double : la première ligne (par id) dont `frais_livraison` est renseigné gagne ; une ligne à
   `frais_livraison` NULL est une ville non tarifée, jamais un tarif à 0 (`LivreurFactureService::grilleLivreur`).
8. Un vendeur ou un livreur sans date de validation Laravel est validé à sa création (Laravel ne le bloque pas).
9. Comptes partenaires API (riyalto, amadex, boraq, trend, livre24, metalivraison, JmproMarrakech, IbexAgadir,
   SoanrpinTanger, Colivraison24) : listés, non importés.
10. Réglages sans équivalent .NET : « afficher les colis à la préparation d'un circuit », « e-mail en copie ».
