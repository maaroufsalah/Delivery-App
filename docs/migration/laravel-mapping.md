# Reprise Wimo Laravel → Wimo .NET : correspondance des tables

Source : base MariaDB 10.4 de Wimo Laravel en production (`wimo-wimo-db`, lecture seule), lue par `mysqldump
--single-transaction` puis `scripts/laravel-dump-to-csv.py`. Cible : base PostgreSQL de Wimo .NET. Outil :
`api/tools/LegacyImport` (logique dans `LaravelImporter` et `LaravelImporter.Operations`). Décisions du référentiel et
des comptes : `docs/chantiers/reprise-wimo-laravel.md`.

## Règles générales

- **Correspondance** : chaque ligne reprise a sa ligne `legacy_mappings` (`entity`, `legacyId` Laravel, `newId` .NET,
  `hash` de l'état voulu). Une relance retrouve la ligne, ne réécrit que si l'empreinte a changé (« mises à jour ») et
  laisse le reste (« identiques »). Sortes : `zone`, `city`, `district`, `agency`, `bank`, `bank_account`, `user`,
  `client`, `courier`, `parcel`, `parcel_event`, `delivery_note`, `route`, `invoice`, `payment`, `courier_statement`,
  `cash_remittance`, `exchange`.
- **Dates** : les horodatages Laravel sont l'heure de Casablanca sans fuseau, convertis en UTC. Une date seule
  (`date_report`, `date_sorti`, `date_paiement`) devient minuit à Casablanca.
- **Lignes supprimées** (`deleted_at`) : non reprises. Les relations Laravel les filtrent déjà (`App\Facture::colis()`,
  `App\Recette::colis()`), les totaux ne changent donc pas.
- **Montants** : ceux qu'affiche Laravel, ses formules appliquées une fois à l'import et stockés (jamais recalculés par
  .NET). Factures, recettes et relevés livreur ne stockent aucun montant dans Laravel : ils sont calculés à l'affichage
  à partir des colis.
- **Verrou** : un document clos dans Laravel (facture validée ou payée, règlement, recette acceptée ou refusée, relevé
  livreur validé, bon validé, circuit clos) porte `importedAt` et la note « Reprise Wimo Laravel » ; ses règles de
  statut .NET l'empêchent déjà d'être modifié, et `ImportedDocument.EnsureEditable` ferme les deux portes restantes
  (suppression d'un règlement, suppression d'un retour de fonds). Un document encore ouvert dans Laravel (facture
  « Non validée », recette « En attente de validation », circuit en cours, bon « Attente de validation ») est repris
  sans `importedAt` : il continue sa vie dans .NET. Un règlement peut être ajouté à une facture importée « Validée & Non
  payée ».
- **Colis non facturés** : un colis livré, refusé ou retourné sans facture Laravel reste « À facturer » dans .NET
  (`billingState = TO_INVOICE`) et entre dans une nouvelle facture .NET.

## Tables reprises

### Référentiel

| Laravel | .NET | Colonnes |
| --- | --- | --- |
| `zones` | `zones` | `label`→`name` (clé) ; `tarif`→`baseRate` ; `tarif_ville`→`sameCityRate` (NULL → `baseRate`) ; `villes` (liste d'ids)→`zone_cities` ; zone « Hors zone » ajoutée |
| `zone_tarifs` | `zone_rates` | `zone_from`, `zone_to`→paire de zones (clé) ; `tarif`→`rate` (NULL : ligne ignorée) ; `delai`→`deliveryDays` |
| `villes` | `cities` | `code`→`code` (clé ; doublon → code dérivé) ; `label`→`name` + `nameAr` (`CityNames.Normalize`) ; `is_active`→`isActive` ; `created_at`→`createdAt` ; `alentours` (identifiant d'une ville voisine) et `legacy_id` : **non repris**, aucune colonne .NET ; zone : celle de `zones.villes`, sinon « Hors zone » |
| `quartiers` | `districts` | `ville_id`+`label` (clé, doublons fusionnés) |
| `tarifs` | `pricing_rules` + `cities.deliveryDelay` | `depart_id`, `destination_id`→paire (clé) ; `prix_quartier`→`rateDistrict`, `prix_agence`→`rateAgency`, `prix_point_relais`→`rateRelayPoint` (NULL = 0) ; `enabled` ignoré (Laravel l'ignore) ; `delai` au départ de la ville de l'agence→`cities.deliveryDelay` de la destination ; `prix`, `depart_time` : non repris (inutilisés par Laravel) |
| `agences` | `agencies` | `label`→`name` (clé) ; `ville_id`, `tel`, `email`, `adresse`, `latitude`, `longitude` ; `image`, `key`, `alias`, `metier_id` : non repris |
| `banques`, `compte_bancaires` | `banks`, `bank_accounts` | libellé (clé) ; `rib` sans espaces |
| `app_configs` 1 à 4 | réglages `pricing.*` | MAD → centimes ; 5 (afficher les colis à la préparation) et 6 (e-mail en copie) : sans équivalent |
| `reclamation_types` | `claim_types` | `label` (clé) ; `type_entite` `App\Colis`→`PARCEL`, sinon `GENERAL` ; `statut_ids`, `reclamation` : non repris |
| `colis_statuts` | catalogue .NET (`parcel_statuses`) | mêmes identifiants, rien n'est écrit (table ci-dessous) |
| `livraison_types` | `delivery_types` | l'unique type « Livraison normale » = le type par défaut .NET |

### Comptes

| Laravel | .NET | Colonnes |
| --- | --- | --- |
| `users` rôle 1 (+ `user_modules`) | `users` SUPER_ADMIN / STAFF | `email` (clé) ; `nom`, `prenom`, `tel`, `cni`, `fonction`, `agence_id` ; `password` (bcrypt `$2y$`) copié tel quel, refait en argon2id à la première connexion ; aucun ou tous les droits → SUPER_ADMIN, partiels → STAFF de son agence ; `wimo@gmail.com` reste l'administrateur principal (rapproché, jamais écrasé) |
| `users` rôle 2 + `clients` | `users` MERCHANT + `clients` | `label`→`name` ; `type` ecommercant/particulier→`type` ; `ice`→`taxId` ; `enabled`→`isActive` ; `agence_id` ; `validation_date`→`validatedAt` ; ville et adresse du compte ; `frais_refus`, `frais_retour` (tous NULL), `prepare_colis` : non repris ; Pro : aucun champ Laravel, `isPro = false` |
| `client_remises` | `client_discounts` | `frais_livraison`→`flatRate`, `meme_ville`→`sameCityRate`, `frais_ramassage`→`pickupFee` ; `enabled` NULL ou 0 → inactive (Laravel n'applique que `enabled = 1`) |
| `client_ramassage_adresses` | `client_pickup_addresses` | adresses identiques fusionnées |
| `users` rôle 4 + `livreurs` | `users` COURIER + `couriers` | identité, ville, agence, véhicule, banque, RIB |
| `livreur_ville` | `courier_cities` | première ligne (par `id`) tarifée par paire livreur + ville : `frais_livraison`→`deliveryFee`, `frais_ramassage`→`pickupFee` ; NULL = ville non tarifée |

### Opérationnel

**`colis` → `parcels`** (code d'origine inchangé)

| Colonne Laravel | Colonne .NET | Règle |
| --- | --- | --- |
| `id` | `legacy_mappings(parcel)` | |
| `code` | `code` | inchangé (même format `ORIG jj rang aa mm DEST` que `buildParcelCode`) ; `ECH-…`, `RET-…` gardés |
| `code_externe` | `externalCode` | un doublon chez le même vendeur est vidé (unicité .NET) et listé |
| `client_id` | `clientId` | vendeur non repris → colis rejeté |
| `livreur_id` | `courierId` | |
| `sys_agence` | `agencyId` | sinon agence du vendeur, sinon agence principale |
| `statut_id` | `statusId` | table des statuts ci-dessous |
| `statut_date` | `statusAt` | sinon `updated_at` |
| `ramassage_ville_id`, `livraison_ville_id` | `originCityId`, `destinationCityId` | ville non reprise → colis rejeté |
| `livraison_id` + `livraison_type` (`App\Quartier`) | `destinationDistrictId`, `destinationDistrictName` | `deliveryMode = DISTRICT` (tous les colis Laravel) |
| `emplacement_id` + `emplacement_type` (`App\Ville`) | `currentCityId` | sinon ville de ramassage |
| `ramassage_id`, `ramassage_type` | — | point de ramassage (agence ou quartier) : aucune colonne .NET, l'adresse suffit |
| `designation`, `destinataire`, `tel_destinataire`, `ramassage_adresse`, `livraison_adresse` | `description`, `recipientName`, `recipientPhone`, `pickupAddress`, `deliveryAddress` | |
| `prix` (texte) | `codAmount` | |
| `frais` | `deliveryFee` | |
| `frais_echoue` | `failureFee` | |
| `frais_inclus`, `deja_paye`, `ramassage_exonere` | `feeIncluded`, `alreadyPaid`, `pickupWaived` | |
| `frais_deja_paye` | — | toujours NULL en production ; compté dans les lignes de facture (formule Laravel) |
| `colis_echoue`, `retour` | `isFailed`, `isReturn` | `RET-` → `kind = RETURN`, `ECH-` → `kind = EXCHANGE` |
| `reference_id` | `referenceParcelId` | |
| `ouverture_colis`, `colis_fragile` | `openOnDelivery`, `fragile` | |
| `date_report` | `scheduledAt` | |
| `latitude`, `longitude` | `latitude`, `longitude` | texte illisible → NULL |
| `type_livraison_id` | `deliveryTypeId` | type par défaut |
| `etat_id` (1 À facturer, 2 Facturé, 3 Remboursé) | `billingState` | **déduit de la facture** (plus sûr que `etat_id`, posé dès le brouillon) : facture payée → `REIMBURSED`, validée → `INVOICED`, brouillon ou aucune → `TO_INVOICE` |
| — | `financialStatus` | facture payée → `rembourse` ; validée → `facture` ; livré et dans une recette acceptée → `remis-livreur` ; livré → `encaisse` ; sinon `non-encaisse` |
| — | `reimbursedByCourier`, `reimbursedAt`, `reimbursedRemittanceId` | première recette acceptée (par date de confirmation) qui contient le colis livré |
| — | `courierCommission` (+ `…At`, `…Source = GRID`, `…CourierId`) | frais de la grille du livreur de cette recette pour la ville de livraison |
| `whstatut_id`, `emplacement_type` autre que Ville, `user_id`, `deleted_at` | — | entrepôt inutilisé (0 entreposage) ; auteur porté par l'historique ; supprimés non repris |

**`colis_trakings` → `parcel_events`** : `statut_id`→`statusId` (tranché ; NULL = commentaire seul → statut du colis à
ce moment), `commentaire`→`comment`, `user_id`→`authorId` + `authorName`, `file`→`attachmentName` (fichier non copié),
`date_report`→`scheduledDate`, `created_at`→`createdAt`. `confirmation*` (confirmation du livreur dans un circuit) →
porté par `route_parcels.confirmed*`. `frais_livreur` → lignes des relevés livreur (factures livreur).

**`bon_livraisons` + `bon_livraison_colis` → `delivery_notes` + `delivery_note_parcels`** : `code` inchangé ;
`type` `ramassage`→`PICKUP`, `envoi` ou NULL→`DROPOFF` ; `statut_id` 1→`AWAITING_VALIDATION`, 2→`VALIDATED`
(`validatedAt = updated_at`, verrouillé) ; `client_id` ; `sys_agence`→agence ; `emplacement_id` (Ville)→`locationCityId`.

**`boxes` + `boxe_colis` → `routes` + `route_parcels`** : `code` inchangé ; `destinataire_id` (`App\Livreur`)→`courierId`
(livreur non repris → circuit rejeté) ; `ville_id`→`cityId` ; `depart_id`→`originCityId` ; `date_sorti`→`exitDate` ;
`type_circuit` livraison→`DELIVERY` ; `statut_id` 5 Brouillon→`DRAFT`, 7→`DELIVERING`, 8→`COLLECTING`, 9→`CLOSED`,
**6 « Dispatched »** → `DISPATCHED` si un colis dont c'est le dernier circuit est encore en cours (groupe initial,
transit, pivot ou relance), sinon `CLOSED` (Laravel ne clôt presque jamais ses circuits : 632 sur 634 sont
« Dispatched »). `boxe_colis.confirmed*`→`confirmed`, `confirmedAt`, `confirmedBy` ; sur le dernier circuit d'un colis
arrivé au bout : `finalStatusId` et, livré, `codCollected` = `codCash` = `Colis::montantAEncaisser`. `partenaire_id`,
`destinataire_type` autre, `ramassage`, `statut_date`→`statusAt`.

**Échanges** : chaque colis `ECH-…` (`reference_id` = colis d'origine) → `exchanges` (`code` = code du colis, colis
d'origine et nouveau colis, produits = `designation`, destinations = adresses, `amount` = `prix`, `fees` = `frais`,
statut : nouveau colis livré + retour `RET-` récupéré → `CLOTURE`, nouveau colis livré → `NOUVEAU_LIVRE`, retour
récupéré → `ANCIEN_RECUPERE`, annulé → `ANNULE`, sinon `NOUVEAU_COLIS_CREE`). Le colis `RET-…` est un colis de
retour (`kind = RETURN`) lié au colis d'origine.

### Argent

**`factures` (client) + `facture_colis` → `invoices` + `invoice_lines`** — formule de `App\Http\Resources\Facture`,
par colis non supprimé de la facture (statut ACTUEL du colis, comme Laravel) :

| Ligne .NET | Laravel |
| --- | --- |
| `codAmount` | livré sans `frais_echoue` : `prix` si `deja_paye != 1` + `frais` si `frais_inclus != 1` (`colisLivres` + `colisLivresFraisNonInclu`) |
| `deliveryFee` | livré sans `frais_echoue` : `frais` (`colisSumFrais`) − `frais` si `frais_deja_paye` (`colisSumFraisPayeEspece`) |
| `failureFee` | `frais_echoue` (`colisSumFraisEchoue`) |
| `netDue` | `codAmount − deliveryFee − failureFee` ; Σ = `prix` de la facture Laravel |

Totaux de la facture = sommes des lignes (`totalCod` = `prix_total`, `totalDeliveryFee + totalFailureFee` =
`frais_total`, `totalDue` = `prix`), `vatRate = 0`, TVA 0 (Laravel n'en calcule pas), `billingType = NORMAL`. `code`
inchangé. `statut_id` 1 Non validée→`DRAFT` (ouvert), 2 Validée & Non payée→`ISSUED`, 3 Facture payée→`PAID`
(`paidAt` = dernier règlement) ; `issuedAt = updated_at`. `sys_agence`→agence.

**`rembourssements` → `payments`** : `facture_id`→`invoiceId` ; `montant`→`amount` ; `mode_id` 1 Espèce→`CASH`,
2 Virement→`TRANSFER`, 3 Chèque→`CHECK` ; `compte_id`→`bankAccountId` ; `reference`, `commentaire`→`note`,
`date_paiement`→`paidAt`, `file`→`proofName` (fichier non copié). Verrouillé.

**`factures` (livreur) + `facture_livreur_colis` → `courier_statements` + `courier_statement_lines`** : une ligne par
ligne d'historique (`traking_id`) : statut 18 → `PICKUP`, sinon `DELIVERY`, `amount = frais_livreur`, `workedAt` =
date de l'historique ; `totalDue = commissionsTotal = variableAmount` = Σ `frais_livreur` ; période = première et
dernière ligne ; `statut_id` 2→`VALIDATED`, 3→`PAID`, 1→`DRAFT`.

**`recettes` + `recette_colis` → `cash_remittances` + `cash_remittance_parcels`** — formule de
`LivreurFactureService::calculer` : seuls les colis livrés comptent ; COD = `Colis::montantAEncaisser` ; frais du
livreur = sa grille (`livreur_ville`, première ligne tarifée) pour la ville de livraison. Ligne : `amount` = `cash` =
COD, `commission` = frais (0 pour un colis non livré, gardé pour la composition) ; `confirmedParcelId` sur la première
recette acceptée du colis (unicité .NET : un colis remis deux fois — 111 cas — ne l'est qu'une fois). Caisse :
`expectedCod`, `expectedCommission`, `expectedNet = amount`, `totalCash` = COD. `confirmation_date` NULL →
`DRAFT` (ouvert ; un seul par livreur dans .NET : un brouillon plus ancien du même livreur → `CANCELLED`, listé),
`confirmation = 1` → `CONFIRMED`, sinon `CANCELLED`. `confirmation_user`→`confirmedById`, `commentaire`→`note`,
`file`→`proofName`, `ville_id`→`cityId`, `code` inchangé.

**Compteurs** : `parcel_code_sequences` (jour, départ, arrivée) posé au plus grand rang des codes importés. Les codes
Laravel des factures, circuits, bons et recettes sont aléatoires (10 caractères, sans numéro) : aucun
`document_sequences` à repositionner, les nouveaux documents .NET (`FA-2611-0001`…) ne peuvent pas les rencontrer.

## Statuts

| Laravel | Libellé | .NET (logistique) | Financier .NET (sans facture) |
| ---: | --- | --- | --- |
| 1 | Attente de ramassage | 1 awaiting-pickup | non-encaisse |
| 2 | En attente d'arrivée | 2 awaiting-arrival | non-encaisse |
| 3 | Colis Ramassée | 3 picked-up | non-encaisse |
| 4 | Livré | 4 delivered | encaisse (remis-livreur si dans une recette acceptée) |
| 5 | Retourné | 5 returned | non-encaisse (frais d'échec facturables) |
| 6 | Refusé | 6 refused | non-encaisse (frais d'échec facturables) |
| 7 | Expedié | 7 shipped | non-encaisse |
| 8 | Hors zone | 8 out-of-zone | non-encaisse |
| 9 | Injoignable | 9 unreachable | non-encaisse |
| 10 | Numéro erroné | 10 wrong-number | non-encaisse |
| 11 | 1er appel Pas de réponse | 11 no-answer-1 | non-encaisse |
| 12 | En cours d'expédition | 12 shipping-in-progress | non-encaisse |
| 13 | En cours de livraison | 13 out-for-delivery | non-encaisse |
| 14 | Reporté | 14 postponed | non-encaisse |
| **15** | **En agence destination** | **16 awaiting-delivery** (au plus proche : arrivé, attend sa livraison) | non-encaisse |
| 16 | En attente de livraison | 16 awaiting-delivery | non-encaisse |
| 17 | En cours de ramassage | 17 pickup-in-progress | non-encaisse |
| 18 | Ramassé par le livreur | 18 picked-up-by-courier | non-encaisse |
| 19 | Annulé par le vendeur | 19 cancelled-by-seller | non-encaisse |
| 20 | En attente de retour | 20 awaiting-return | non-encaisse |
| 21 | En cours de récupération | 21 recovery-in-progress | non-encaisse |
| 22 | Récupéré par livreur | 22 recovered-by-courier | non-encaisse |
| 23 | Retour récupéré | 23 return-collected | non-encaisse |
| 24 | En cours de retour | 24 return-in-progress | non-encaisse |
| 25 | Boite vocale | 25 voicemail | non-encaisse |
| 26 | Client en voyage | 26 customer-travelling | non-encaisse |
| 27 | Annulé par le client | 27 cancelled-by-customer | non-encaisse |
| 28 | Client déjà livré | 28 already-delivered | non-encaisse |
| 29 | Pas intéressé | 29 not-interested | non-encaisse |
| **30** | **Au guichet Amana** | **7 shipped** (au plus proche : remis au transporteur) | non-encaisse |
| 31 | En stock | 31 in-stock | non-encaisse |
| 32 | 2eme appel Pas de réponse | 32 no-answer-2 | non-encaisse |
| 33 | 3eme appel Pas de réponse | 33 no-answer-3 | non-encaisse |
| 34 | 1er appel Pas de réponse + SMS | 34 no-answer-1-sms | non-encaisse |
| 35 | 2eme appel Pas de réponse + SMS | 35 no-answer-2-sms | non-encaisse |
| **36** | **3eme appel Pas de réponse + SMS** | **33 no-answer-3** (au plus proche : même relance, sans la mention SMS) | non-encaisse |
| 37 | Envoi erroné (Faute destination) | 37 wrong-destination | non-encaisse |
| 38 | Programmé | 38 scheduled | non-encaisse |

Quel que soit le statut, un colis d'une facture validée est `facture`, d'une facture payée `rembourse`.
Tranchés au plus proche : 15, 30, 36 (en production le 2026-11-28 : 0, 0 et 4 colis, tous de statut 36). Les lignes
d'historique de ces statuts suivent la même règle.

## Tables non reprises

| Table | Raison |
| --- | --- |
| `actualites` (1) | annonce Laravel datée ; .NET a ses propres annonces |
| `app_modules`, `user_modules` | lus pour le rôle du personnel, pas de table .NET équivalente (matrice de rôles .NET) |
| `roles` (0), `vehicule_types` | vides ou remplacés par des énumérations .NET |
| `bon_livraison_statuts`, `boxe_statuts`, `colis_etats`, `colis_wh_statuts`, `facture_statuts`, `entreposage_statuts`, `reclamation_statuts`, `bon_retour_statuts`, `paiement_modes` | catalogues de statuts : énumérations .NET (correspondances ci-dessus) |
| `bon_retours`, `bon_retour_colis`, `bon_retour_livreur_colis`, `boxe_bon`, `reclamations`, `reclamation_trakings`, `entreposages`, `entreposage_produit`, `produits`, `produit_*`, `colis_produit`, `client_boutiques`, `client_documents`, `contacts`, `ecom_solutions`, `points_relais`, `partenaires`, `partenaire_*`, `notifications`, `fcm_tokens`, `failed_jobs` | **vides en production** (0 ligne) ; chèques : aucune table Laravel, aucun règlement « Chèque » |
| `partner_endpoints`, `partner_outbox` | file d'envoi vers les partenaires API (Speedy) : technique, rejouée par .NET avec ses propres jetons |
| `general_configs` | libellés des réglages `app_configs` |
| `password_resets` | jetons de réinitialisation expirés |
| `migrations`, `_backfill_livreur_role_20260807` | tables techniques Laravel |
| `users` partenaires API (riyalto, amadex…) | comptes techniques, listés au compte rendu |
| fichiers (`colis_trakings.file`, `rembourssements.file`, `recettes.file`, images) | noms repris, fichiers non copiés (stockage Laravel hors base) |
