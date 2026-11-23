import type { CourierCommissionSource } from '../courier-pay';
import type {
  BillingState,
  CashRemittanceStatus,
  DeliveryTarget,
  ExpectedPayment,
  InvoiceStatus,
  ParcelKind,
  RouteKind,
  RouteStatus,
} from '../enums';
import type { ChequeStatus } from './cheque';
import type { DashboardPeriod, ResolvedDashboardWindow } from '../dashboard-period';
import type { ParcelExchangeLink } from './exchange';
import type { FinancialStatus, MerchantFinancialStatus } from '../financial-statuses';
import type { AmountInCents, Id, IsoDateTime } from './common';
import type { ImportRowError } from './referential';

/**
 * DOMAIN.md §1.9 — entité centrale.
 * Tous les montants sont en centimes entiers (dette n°2 : `colis.prix` était un varchar).
 */
export interface Parcel {
  id: Id;
  /** Code métier, format DOMAIN.md §3.2. Unique — contrainte absente du legacy (dette n°6). */
  code: string;
  /** Référence commande côté e-commerçant. */
  externalCode: string | null;

  clientId: Id;
  courierId: Id | null;
  agencyId: Id;

  statusId: number;
  statusAt: IsoDateTime;
  billingState: BillingState;

  originCityId: Id;
  destinationCityId: Id;
  destinationDistrictId: Id | null;
  deliveryTarget: DeliveryTarget;
  /** Ville où le colis se trouve physiquement (legacy `emplacement_id`). */
  currentCityId: Id;

  description: string;
  recipientName: string;
  recipientPhone: string;
  pickupAddress: string;
  deliveryAddress: string;

  /** Montant à encaisser à la livraison. */
  codAmount: AmountInCents;
  deliveryFee: AmountInCents;
  failureFee: AmountInCents | null;
  /**
   * LES AUTRES FRAIS — chantier « Finance », point 2.
   *
   * Emballage, attente au pas de porte, seconde présentation. Zéro et non
   * `null` : « pas d'autres frais » et « zéro franc » sont la même chose, à la
   * différence de `failureFee` qui peut n'avoir jamais été renseigné.
   */
  otherFees: AmountInCents;
  /** DOMAIN.md §0.3 — vrai sur 99,8 % des colis prod : le COD inclut les frais. */
  feeIncluded: boolean;
  alreadyPaid: boolean;
  /** DOMAIN.md §0.3 — exonération de ramassage, cas majoritaire (70,6 %). */
  pickupWaived: boolean;

  /**
   * LA NATURE DU COLIS — chantier « Étiquette orange », point 2.
   *
   * `parcel` par défaut ; `return` et `exchange` naissent ensemble, d'un même
   * geste sur un colis LIVRÉ. C'est ce que la liste filtre et ce que la ligne
   * affiche — `isReturn`, juste en dessous, n'a jamais été écrit par aucun
   * geste, et un booléen ne dit pas trois choses.
   */
  kind: ParcelKind;
  isReturn: boolean;
  isFailed: boolean;
  referenceParcelId: Id | null;

  openOnDelivery: boolean;
  fragile: boolean;
  /**
   * « BL à récupérer lors de la livraison ? » — chantier « Retours, BL Clients
   * Pro », point 5.
   *
   * C'est lui qui fait apparaître le bandeau « 📄 BL à récupérer : OUI » sur le
   * téléphone du livreur, et la question « BL récupéré ? » à la livraison.
   * Toujours `false` pour un client qui n'est pas Pro (D-099).
   */
  blRequested: boolean;
  /**
   * LE NOMBRE DE CARTONS — lot G (D-208). 1 par défaut ; au-delà pour tout
   * client depuis D-264. `deliveryFee` porte le TOTAL (unitaire × `quantity`). Absent : 1
   * (le jeu de démonstration ne le porte pas).
   */
  quantity?: number;
  /**
   * « Paiement attendu : Espèces / Chèque » — recette SpeedyTrans 3, point 3.
   * Renseigné pour un client Pro seulement, `null` sinon (et pour les colis
   * d'avant le chantier). Information pour le livreur, imprimée sur
   * l'étiquette : le flux d'encaissement ne change pas.
   */
  expectedPayment: ExpectedPayment | null;
  /** Date de report / RDV (legacy `date_report`). */
  scheduledAt: IsoDateTime | null;

  /**
   * Le livreur a rendu le cash de ce colis — C11 bloc 2, docs/DECISIONS.md D-060.
   *
   * **ABSENT de la réponse pour un e-commerçant.** Ce n'est pas un champ à
   * `false` : les trois clés ne sont pas sérialisées du tout, pour qu'aucune
   * lecture côté client — un `in`, un `??`, une colonne d'export — ne puisse en
   * déduire quoi que ce soit. C'est un état de trésorerie INTERNE entre la
   * société et son livreur ; l'e-commerçant est payé par sa facture, et
   * l'exposer lui donnerait un levier sur une conversation qui ne le regarde
   * pas.
   *
   * Un ATTRIBUT, pas un statut : le colis reste « livré ». Le legacy mélangeait
   * transport et facturation dans `colis.statut_id`, et la colonne avait fini
   * par ne plus rien vouloir dire (§4 dette n°7).
   */
  reimbursedByCourier?: boolean;
  reimbursedAt?: IsoDateTime | null;
  reimbursedRemittanceId?: Id | null;

  /**
   * OÙ EN EST L'ARGENT DE CE COLIS — chantier « Finance », point 1, D-105.
   *
   * L'axe FINANCIER, à côté de `statusId` qui porte l'axe logistique. Aucune
   * valeur de l'un n'implique une valeur de l'autre : un colis livré peut
   * n'être pas encore encaissé, et un colis facturé reste livré.
   *
   * **Absent de la réponse pour un e-commerçant**, comme ses trois voisines et
   * pour la même raison (D-060) : deux de ses cinq marches — `encaisse` et
   * `remis-livreur` — disent où est le cash entre le pas de porte et le coffre
   * de l'agence. Le vendeur garde `billingState`, qui ne porte que les marches
   * qui le concernent.
   */
  financialStatus?: FinancialStatus;
  /**
   * LE MÊME AXE, VU PAR LE VENDEUR — lot O, D-227 : Non encaissé, Encaissé,
   * Facturé, Payé (`merchantFinancialStatusOf`). Servi au SEUL e-commerçant ;
   * absent pour le comptoir, qui lit `financialStatus`.
   */
  merchantFinancialStatus?: MerchantFinancialStatus;

  createdAt: IsoDateTime;
  updatedAt: IsoDateTime;
}

/**
 * DOMAIN.md §1.11 — journal append-only des changements de statut.
 *
 * La table legacy `colis_trakings` mélangeait deux natures : 19 570 changements
 * de statut et 3 456 commentaires libres. Elle est scindée ici en `ParcelEvent`
 * (statut, obligatoire) et `ParcelNote` (commentaire seul).
 *
 * Immuable : `ColisController::delete_traking` supprimait physiquement une
 * ligne d'historique sans laisser de trace. La v2 n'expose aucune suppression.
 */
export interface ParcelEvent {
  id: Id;
  parcelId: Id;
  statusId: number;
  comment: string | null;
  /** Auteur : `null` pour les événements automatiques (cron, API partenaire). */
  authorName: string | null;
  cityId: Id | null;
  /** Pièce jointe (legacy `colis_trakings.file`) : preuve de livraison, photo de refus. */
  attachmentName: string | null;
  /** Date de report saisie avec l'événement (legacy `date_report`). */
  scheduledDate: IsoDateTime | null;
  createdAt: IsoDateTime;
  /**
   * L'étape BL de l'événement, pour le VENDEUR seulement — lot W3, SC-004
   * (D-238). Le BL écrit au statut courant (« Livré ») ; sans commentaire, le
   * vendeur lisait « Livré » répété. L'écran affiche le libellé de l'étape.
   */
  blStep?: BlParcelStep | null;
}

/** Les étapes d'un BL telles que le vendeur les lit dans l'historique du colis. */
export const BL_PARCEL_STEPS = [
  'collected',
  'handed-over',
  'received',
  'returned',
  'closed',
  'corrected',
] as const;
export type BlParcelStep = (typeof BL_PARCEL_STEPS)[number];

/**
 * DOMAIN.md §1.11 — commentaire libre, sans changement de statut.
 * Interne : jamais exposé au suivi public (`/track/[code]`).
 */
export interface ParcelNote {
  id: Id;
  parcelId: Id;
  body: string;
  authorName: string;
  attachmentName: string | null;
  createdAt: IsoDateTime;
}

/**
 * Lequel des montants a bougé — C11 bloc 4, élargi au chantier « Finance »,
 * point 2.
 *
 * Les noms sont ceux du CONTRAT, pas ceux de l'enum de base : le journal est lu
 * par des écrans, et `deliveryFee` s'y retrouve sans traduction. Les deux
 * valeurs d'origine ont changé de forme (`delivery` → `deliveryFee`) ; la
 * migration recopie l'historique dans le nouveau vocabulaire, pour qu'il n'y en
 * ait qu'un.
 */
export const PARCEL_FEE_FIELDS = ['codAmount', 'deliveryFee', 'failureFee', 'otherFees'] as const;
export type ParcelFeeField = (typeof PARCEL_FEE_FIELDS)[number];

/**
 * Une modification de frais, telle qu'elle se lit sur la fiche — D-063.
 *
 * Ancien, nouveau, motif, qui, quand : les cinq colonnes que le PO a
 * demandées, et les cinq qui manquaient au legacy pour trancher une
 * contestation autrement qu'à la parole.
 */
export interface ParcelFeeChange {
  id: Id;
  parcelId: Id;
  field: ParcelFeeField;
  /** `null` quand les frais d'échec n'étaient pas encore renseignés. */
  oldAmount: AmountInCents | null;
  newAmount: AmountInCents;
  reason: string;
  authorName: string;
  createdAt: IsoDateTime;
}

/**
 * UNE LIGNE DU JOURNAL DES EXPLICATIONS — chantier « Finance », point 2, D-109.
 *
 * La forme générale dont `ParcelFeeChange` est le cas particulier : n'importe
 * quelle entité, n'importe quel champ, et un motif FACULTATIF — obligatoire
 * chez ceux qui l'exigent (le changement de destination, point 7), libre
 * ailleurs. L'imposer partout aurait fait écrire « RAS » partout.
 *
 * Les valeurs sont du TEXTE : un journal qui accueille un montant, une ville et
 * un numéro de chèque ne peut pas être typé sur l'un des trois. Les montants y
 * entrent en centimes, comme partout.
 */
export interface FieldChange {
  id: Id;
  entity: string;
  entityId: Id;
  field: string;
  oldValue: string | null;
  newValue: string;
  reason: string | null;
  authorName: string;
  at: IsoDateTime;
  /**
   * D-257 : les valeurs NOMMÉES quand le champ désigne une fiche (client,
   * ville, agence) — l'historique dit « rania », pas un identifiant. Absentes
   * pour un champ texte ou un montant, qui se lisent tels quels.
   */
  oldLabel?: string | null;
  newLabel?: string | null;
}

/** Vue enrichie servie aux écrans de liste — jointures résolues côté API. */
export interface ParcelListItem extends Parcel {
  clientName: string;
  courierName: string | null;
  originCityName: string;
  destinationCityName: string;
  destinationDistrictName: string | null;
  /**
   * Les issues que CE livreur peut poser sur le colis — 21/11/2026. Présent
   * pour un livreur seulement (absent pour tout autre rôle) ; même règle que
   * `GET /courier/parcels/:code/outcomes`, calculée pour toute la page en deux
   * requêtes. Vide : le colis n'est pas en main.
   */
  courierOutcomes?: number[];
}

/**
 * Ce que le colis CONTIENT, quand il vient de l'entrepôt.
 *
 * Rempli par C5 à la création du colis depuis une commande (`ParcelItem`), vide
 * pour un colis saisi à la main : tout le monde n'entrepose pas chez nous.
 */
export interface ParcelContentLine {
  productId: Id;
  productName: string;
  productReference: string | null;
  quantity: number;
}

/**
 * LES CIRCUITS QUI ONT PORTÉ CE COLIS — chantier « Finance », point 11.
 *
 * Au pluriel, parce qu'un colis en traverse souvent plusieurs : une tournée
 * dont il est ressorti faute de destinataire, une seconde présentation le
 * lendemain, un ramassage avant tout cela. N'en montrer qu'un — le dernier —
 * aurait effacé la moitié de l'histoire que la fiche est censée raconter.
 */
export interface ParcelRouteLink {
  routeId: Id;
  code: string;
  kind: RouteKind;
  status: RouteStatus;
  courierName: string;
  cityName: string;
  exitDate: IsoDateTime | null;
  /** La course a-t-elle été confirmée, et quand. */
  confirmed: boolean;
  confirmedAt: IsoDateTime | null;
  /** L'issue posée par le livreur sur CETTE course. */
  finalStatusId: number | null;
}

/**
 * TOUT L'ARGENT DE CE COLIS, EN UN OBJET — chantier « Finance », point 11,
 * D-113.
 *
 * **Une vue, pas un second statut.** Rien ici ne décrit où est le carton :
 * l'axe logistique vit sur `statusId`, et il n'a pas d'équivalent dans cet
 * objet. C'est ce que « aucun mélange entre statut logistique et financier »
 * veut dire quand on l'écrit dans un type plutôt que dans une note (D-105).
 *
 * **Absente de la réponse pour un e-commerçant** (D-060), entièrement et non
 * par champs : elle porte la commission du livreur, sa caisse et le détail du
 * paiement — la conversation interne entre la société et son livreur. Le
 * vendeur a sa facture, qui dit ce qui le regarde.
 */
export interface ParcelFinanceView {
  /** Ce qu'il FALLAIT encaisser au pas de porte — DOMAIN.md §3.8. */
  toCollect: AmountInCents;
  /**
   * Ce qui a été encaissé, figé à la confirmation de la course.
   *
   * `null` tant qu'aucune course n'est confirmée — distinct de `0`, qui est un
   * encaissement nul et bien réel (D-101).
   */
  collected: AmountInCents | null;
  /** `cash` + `cheque` = `collected`. Les trois viennent de la base (D-108). */
  cash: AmountInCents | null;
  cheque: AmountInCents | null;
  chequeNumber: string | null;
  chequeStatus: ChequeStatus | null;

  /** La commission du livreur, FIGÉE à la course (D-059, D-065). */
  commission: AmountInCents | null;
  commissionSource: CourierCommissionSource | null;

  /** Le retour de fonds qui porte ce colis (D-115). */
  remittanceId: Id | null;
  /** Son NUMÉRO — c'est lui qu'on montre, pas un identifiant technique. */
  remittanceCode: string | null;
  remittanceStatus: CashRemittanceStatus | null;

  /** La facture de l'e-commerçant qui porte ce colis (D-036). */
  invoiceCode: string | null;
  invoiceStatus: InvoiceStatus | null;
  invoiceIssuedAt: IsoDateTime | null;
  invoicePaidAt: IsoDateTime | null;
  /** La ligne GELÉE : ce que la facture compte pour ce colis. */
  invoiceNetDue: AmountInCents | null;

  /** Le bon de versement du COD, pour un e-commerçant Pro (D-061). */
  payoutCode: string | null;
  payoutPaidAt: IsoDateTime | null;

  /**
   * CE QUI A ÉTÉ REMBOURSÉ à l'e-commerçant pour ce colis.
   *
   * Deux chemins mènent au même montant, et un seul est emprunté par colis : le
   * **bon de versement** pour un client Pro, dont le COD part à part de la
   * facture ; la **ligne de facture réglée** pour tous les autres. `null` tant
   * que rien n'est parti — un montant à zéro dirait « remboursé zéro », ce qui
   * n'est pas la même chose.
   */
  reimbursedAmount: AmountInCents | null;
}

export interface ParcelDetail extends ParcelListItem {
  /**
   * D-257 : posé quand le colis est SUPPRIMÉ — sa fiche ne s'ouvre alors qu'à
   * l'administrateur, en lecture seule, avec son historique.
   */
  deletedAt?: IsoDateTime | null;
  /**
   * CE QUE COÛTERAIT LE RETOUR DE CE COLIS — chantier « Étiquette orange »,
   * point 2 : « frais de retour affichés ».
   *
   * Servi par l'API et non lu à l'écran, parce que c'est le VENDEUR qui doit le
   * voir avant de cliquer — et qu'il n'a pas `settings.read`. Un tarif qu'il
   * découvre sur sa facture est une réclamation.
   *
   * C'est le tarif d'ÉCHEC (DOMAIN.md §3.3) : le colis revient sans avoir été
   * livré. Il vient des réglages métier, par agence (D-019), et l'API le GÈLE
   * sur le colis de retour à la création — l'afficher ici et le recalculer là
   * aurait fini par donner deux chiffres.
   */
  returnFee: AmountInCents;

  events: ParcelEvent[];
  notes: ParcelNote[];
  /**
   * L'historique des frais — C11 bloc 4, D-063.
   *
   * Il voyage avec le détail plutôt que derrière sa propre route : il tient en
   * quelques lignes, il se lit sur la fiche, et un onglet qui déclenche un
   * second aller-retour pour trois lignes n'est pas un onglet, c'est une
   * attente.
   */
  feeChanges: ParcelFeeChange[];
  /**
   * TOUT ce qui a été expliqué sur ce colis — chantier « Finance », point 2.
   *
   * `feeChanges` en est la moitié monétaire, gardée telle quelle pour l'écran
   * qui la lit déjà. Celle-ci porte aussi ce que les blocs suivants
   * journalisent : une destination changée, un échange ouvert.
   */
  fieldChanges: FieldChange[];
  /**
   * LES ÉCHANGES de ce colis — chantier « Finance », point 6.
   *
   * « Relation navigable Colis original ↔ Retour ↔ Nouveau colis dans les trois
   * fiches » : chaque lien dit ce que CE colis est dans le dossier, l'origine
   * ou le neuf, et nomme l'autre. Sans le rôle, la fiche afficherait un code
   * sans savoir s'il désigne ce qui part ou ce qui revient.
   */
  exchanges: ParcelExchangeLink[];
  /**
   * LES CIRCUITS QUITTÉS — chantier « Finance », point 7.
   *
   * Renseigné par le seul changement de destination, et vide partout ailleurs.
   * Quand la ville change, le colis SORT des tournées où il attendait : un
   * livreur de Casablanca ne porte pas un colis qui part à Rabat. Le taire
   * aurait laissé l'agent croire que le livreur l'a encore.
   */
  droppedFromRoutes?: string[];
  /** Les frais AVANT et APRÈS un recalcul de tarif. `null` s'il n'y en a pas eu. */
  repricedFrom?: AmountInCents | null;
  repricedTo?: AmountInCents | null;
  /**
   * Le numéro de la facture ÉMISE qui porte ce colis, s'il y en a une.
   *
   * C'est ce qui VERROUILLE les frais (D-063) : l'écran grise la modification
   * et dit sur quelle facture aller, plutôt que de laisser l'agent découvrir
   * le 409 après avoir saisi son motif.
   */
  invoicedOn: string | null;
  /**
   * Le numéro de la facture RÉGLÉE qui porte ce colis — chantier « Finance »,
   * point 2.
   *
   * C'est LUI qui verrouille désormais les montants, et non `invoicedOn`.
   * L'émission ne fige plus rien côté colis : les lignes de facture sont des
   * copies gelées (D-036), et corriger le colis ne touche pas la pièce
   * comptable. Le règlement, lui, a fait changer de l'argent de mains — et
   * cela ne se réécrit pas, cela se corrige par un avoir.
   */
  invoiceSettledOn: string | null;
  agencyName: string;
  /** Montant réellement à encaisser au pas de porte — DOMAIN.md §3.8. */
  codToCollect: AmountInCents;
  /**
   * CE QUI A ÉTÉ ENCAISSÉ — lot W3, SC-005 (D-238). Colis livré et encaissé :
   * le montant de la course et sa répartition espèces / chèque, lus par TOUS
   * (le vendeur y lit son argent, rien de la caisse du livreur, D-060).
   * `null` tant que rien n'est encaissé : la fiche dit « À encaisser ».
   */
  collected?: { amount: AmountInCents; cash: AmountInCents; cheque: AmountInCents } | null;
  /** Contenu entreposé, quand le colis vient d'une commande (C5). */
  items: ParcelContentLine[];
  /**
   * LA VUE UNIQUE — chantier « Finance », point 11, D-113.
   *
   * Tout l'argent du colis en un objet, à côté de la logistique que porte déjà
   * le reste de la fiche. Absente pour un e-commerçant (D-060).
   */
  finance?: ParcelFinanceView;
  /**
   * Les circuits qui ont porté ce colis — point 11.
   *
   * Vide pour un e-commerçant : la tournée d'un livreur ne le regarde pas plus
   * que sa caisse.
   */
  routes: ParcelRouteLink[];
}

export interface ParcelFilters {
  search?: string;
  /** `'true'` : les colis supprimés seuls — D-257, l'administrateur seul. */
  deleted?: 'true';
  /**
   * Un statut (`4`), ou plusieurs tels que l'URL les porte (`'4,5'`) — D-255 :
   * les filtres de liste sont à choix multiple, l'API lit la liste.
   */
  statusId?: number | string;
  statusGroup?: string;
  /** Plusieurs statuts, `4,5,23` — les compteurs du tableau de bord (lot E). */
  statusIds?: string;
  clientId?: Id;
  courierId?: Id;
  destinationCityId?: Id;
  billingState?: BillingState;
  /** L'axe financier — point 1. Il se combine au statut logistique, il ne le remplace pas. */
  financialStatus?: FinancialStatus;
  /** Le filtre financier du vendeur — lot O, D-227. Honoré pour le seul e-commerçant. */
  merchantFinancialStatus?: MerchantFinancialStatus;
  /**
   * Le TYPE de colis — chantier « Étiquette orange », point 2 : colis, retour
   * ou échange. Un troisième axe, indépendant des deux autres.
   */
  kind?: ParcelKind;
  /**
   * LA PÉRIODE — chantier « Retours recette 26/09 », point 1.
   *
   * Le même vocabulaire que les tableaux de bord (`DASHBOARD_PERIODS`), posé
   * sur la date de CRÉATION du colis. ABSENT = aucune borne : les autres
   * lecteurs de la liste (espace livreur, bons) ne filtrent rien, et l'écran
   * colis passe explicitement « depuis le début » pour ne jamais masquer un
   * colis par défaut.
   */
  period?: DashboardPeriod;
  /** Bornes INCLUSES `AAAA-MM-JJ`, lues seulement quand `period === 'custom'`. */
  from?: string;
  to?: string;
}

/** KPI de tête de page — calculés côté API en cible. */
export interface ParcelStats {
  total: number;
  delivered: number;
  inTransit: number;
  failed: number;
  /** Somme des COD encaissés sur les colis livrés. */
  collectedCod: AmountInCents;
  /** Livrés / (livrés + échoués terminaux), en pourcentage. */
  deliveryRate: number;
  /**
   * La fenêtre RÉSOLUE quand une période a été demandée — point 1. C'est elle
   * que le sélecteur affiche : l'écran ne recalcule aucune borne, comme sur
   * les tableaux de bord. Absente quand la liste n'est pas bornée.
   */
  window?: ResolvedDashboardWindow;
}

/* -------------------------------------------------------------------------- */
/* Import Excel — C7 bloc 1                                                   */
/* -------------------------------------------------------------------------- */

/**
 * Les douze colonnes du classeur d'import, dans l'ordre du modèle téléchargé.
 *
 * C'est la MÊME liste qui nomme les cellules de l'aperçu, les colonnes du
 * modèle et les champs corrigeables à l'écran : un ajout de colonne se fait
 * ici, et le reste suit ou cesse de compiler.
 */
export const PARCEL_IMPORT_FIELDS = [
  'externalCode',
  'recipientName',
  'recipientPhone',
  'city',
  'district',
  'address',
  'cod',
  'description',
  /*
   * « Frais inclus » n'est plus une colonne — dernières corrections, point 3 :
   * les frais sont toujours inclus. Un ancien classeur qui la porte encore
   * passe : la colonne n'est simplement plus lue.
   */
  'openOnDelivery',
  'fragile',
  'notes',
  // Lot G (D-208) : EN FIN, facultative — un ancien classeur sans elle reste valide.
  'quantity',
] as const;

export type ParcelImportField = (typeof PARCEL_IMPORT_FIELDS)[number];

/** Une ligne telle qu'elle a été LUE : douze chaînes, rien d'interprété. */
export type ParcelImportValues = Record<ParcelImportField, string>;

/**
 * Une ligne après validation.
 *
 * Les valeurs restent des CHAÎNES même quand elles sont valides : l'écran les
 * réaffiche dans des cellules éditables, et l'opérateur corrige ce qu'il a
 * tapé, pas une projection normalisée qu'il ne reconnaîtrait pas.
 */
export interface ParcelImportRow {
  /** Numéro de ligne dans le fichier, en-tête comprise. Ligne 1 = en-tête. */
  row: number;
  values: ParcelImportValues;
  errors: ImportRowError[];
  /**
   * Les AVERTISSEMENTS — lot G (D-208) : la ligne passe, mais une saisie a été
   * ignorée (« Quantité ignorée : réservée aux clients pro »). Ils ne rendent
   * jamais une ligne invalide.
   */
  warnings?: ImportRowError[];
  /** Frais de livraison estimés, quand la ligne est valide. */
  fee: AmountInCents | null;
}

export interface ParcelImportPreview {
  /** Pour qui l'import se fait — imposé par la portée si le compte est marchand. */
  clientId: Id;
  clientName: string;
  /** Ville de départ retenue : l'adresse de ramassage par défaut du client. */
  originCityName: string;
  rows: number;
  valid: number;
  invalid: number;
  /** Toutes les lignes, dans l'ordre du fichier. C'est le tableau qu'on édite. */
  items: ParcelImportRow[];
  /** Somme des frais estimés sur les lignes valides. */
  estimatedFees: AmountInCents;
}

export type ParcelImportMode = 'all-or-nothing' | 'valid-only';

export interface ParcelImportResult {
  importId: Id;
  created: number;
  /** Lignes refusées — nul en mode tout-ou-rien, qui échoue plutôt qu'il ne trie. */
  skipped: number;
  /** Lignes importées avec un avertissement (lot G). */
  warnings?: number;
    /** Codes des colis créés, dans l'ordre du fichier. */
  codes: string[];
}

export interface ParcelImportListItem {
  id: Id;
  filename: string;
  clientId: Id;
  clientName: string;
  authorName: string;
  mode: ParcelImportMode;
  rowCount: number;
  okCount: number;
  koCount: number;
  createdAt: IsoDateTime;
}
