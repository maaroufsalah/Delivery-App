import type {
  ReturnHandoverOrigin,
  ReturnHandoverStatus,
  ReturnReason,
  ReturnSlipStatus,
} from '../enums';
import type { AmountInCents, Id, IsoDateTime } from './common';

/**
 * Contrats du menu RETOURS — DOMAIN.md §1.14.
 *
 * Le retour se joue en DEUX temps, et ces deux temps n'ont ni les mêmes
 * acteurs, ni les mêmes preuves :
 *
 *  1. **La remise** (`ReturnHandover`, code `RL-`) : le livreur rapporte à
 *     l'agence les colis qu'il n'a pas pu livrer. C'est un geste interne, et
 *     c'est le pivot legacy `bon_retour_livreur_colis` — 811 lignes en prod.
 *  2. **Le bon de retour** (`ReturnSlip`, code `BR-`) : l'agence rend les colis
 *     à l'e-commerçant, contre le nom du receveur. C'est un geste externe, et
 *     c'est le pivot `bon_retour_colis` — 5 lignes en prod.
 *
 * `DECISIONS.md` D-025 dit pourquoi ce sont deux objets et non un seul portant
 * un `leg`, comme le proposait `DOMAIN.md §1.14`.
 */

/** Ligne de colis commune aux deux écrans de retour. */
export interface ReturnParcelLine {
  id: Id;
  code: string;
  /** La référence commande du vendeur, cherchable au comptoir — point 7 du 26/09. */
  externalCode: string | null;
  clientId: Id;
  clientName: string;
  recipientName: string;
  /**
   * Le TÉLÉPHONE du destinataire — retouches Anass, point 7.
   *
   * « Recherche par code / destinataire / téléphone. » Le filtre est appliqué
   * au serveur, mais la ligne le porte quand même : au comptoir, c'est le
   * numéro qu'on a sous les yeux, et le reconnaître dans la liste vaut mieux
   * que de refaire une recherche pour vérifier qu'on tient le bon colis.
   */
  recipientPhone: string;
  destinationCityName: string;
  description: string;
  statusId: number;
  codAmount: AmountInCents;
  /**
   * LES FRAIS ET LE LIVREUR — chantier « Retours, BL Clients Pro », point 2.
   *
   * « Liste des colis éligibles chez ce livreur (code, vendeur, destinataire,
   * statut, date de livraison/tentative, montant, frais, livreur). » Les six
   * premiers étaient déjà là ; ces deux-ci manquaient.
   *
   * Le livreur figure sur la ligne alors que la liste est déjà bornée à UN
   * livreur, et ce n'est pas redondant : la même ligne sert la pile d'entrée
   * (`/returns/pending`), qui les mélange.
   */
  deliveryFee: AmountInCents;
  courierName: string | null;
  /** Depuis quand le colis attend — c'est ce qui rend une pile urgente. */
  statusAt: IsoDateTime;
  /** Nombre de cartons sous ce code (lot G, D-208) — affiché sur le mobile, jamais compté comme des colis. */
  quantity?: number;
}

/** Colis à retourner, groupés par livreur : l'écran `/returns/pending`. */
export interface ReturnPendingGroup {
  courierId: Id | null;
  courierName: string | null;
  agencyId: Id | null;
  agencyName: string | null;
  parcels: ReturnParcelLine[];
}

/* --- Remise du livreur à l'agence ---------------------------------------- */

export interface ReturnHandoverItem {
  id: Id;
  parcelId: Id;
  parcelCode: string;
  clientName: string;
  recipientName: string;
  statusId: number;
  note: string | null;
  /** Date de création du COLIS : l'écran trie dessus, récent d'abord (chantier « Tri par défaut »). */
  parcelCreatedAt?: IsoDateTime;
  /** Nombre de cartons sous ce code (lot G, D-208) — affiché, jamais compté comme des colis. */
  quantity?: number;
  /** « CORRIGÉ DEPUIS » — D-250 : le colis a été corrigé après la réception de ce bon. */
  correctedAt?: IsoDateTime | null;
}

/**
 * UNE MARCHE DU PARCOURS — point 1 : « chaque transition journalisée (date,
 * heure, utilisateur) ».
 *
 * L'état courant dit où en est le bon ; cette suite dit comment il y est
 * arrivé, y compris par les chemins qui ne laissent pas de colonne — une
 * annulation après une prise en charge, une réception faite sans dépôt
 * préalable. Un état ne raconte pas un litige ; une suite d'événements, si.
 */
export interface ReturnHandoverStep {
  id: Id;
  status: ReturnHandoverStatus;
  comment: string | null;
  authorName: string | null;
  createdAt: IsoDateTime;
}

export interface ReturnHandoverListItem {
  id: Id;
  code: string;
  status: ReturnHandoverStatus;
  /** ADMIN ou LIVREUR — ce n'est pas qu'un affichage, cf. `enums.ts`. */
  origin: ReturnHandoverOrigin;
  /** « Modification autorisée par le livreur », sur un bon de l'admin. */
  courierCanEdit: boolean;
  courierId: Id;
  courierName: string;
  agencyId: Id;
  agencyName: string;
  parcelCount: number;
  /**
   * LES VENDEURS du bon, déduits de ses colis — point 1, le PDF les demande.
   *
   * Un bon de retour n'appartient à aucun e-commerçant : il rassemble ce qu'UN
   * livreur rapporte, et cela traverse plusieurs vendeurs. D'où le pluriel, et
   * d'où l'absence de `clientId` sur le bon.
   */
  clientNames: string[];
  createdByName: string | null;
  pickedUpAt: IsoDateTime | null;
  handedOverAt: IsoDateTime | null;
  confirmedAt: IsoDateTime | null;
  confirmedByName: string | null;
  closedAt: IsoDateTime | null;
  cancelledAt: IsoDateTime | null;
  createdAt: IsoDateTime;
}

export interface ReturnHandoverDetail extends ReturnHandoverListItem {
  note: string | null;
  cancelReason: string | null;
  items: ReturnHandoverItem[];
  /** Le journal des transitions, du plus ancien au plus récent. */
  steps: ReturnHandoverStep[];
  /**
   * CE QUE L'ÉCRAN A LE DROIT DE PROPOSER, calculé par le serveur.
   *
   * Les cinq drapeaux sont déduits de l'état, de l'origine et de la portée de
   * l'appelant. Les recalculer à l'écran supposerait d'y recopier la machine à
   * états, et les trois espaces — admin, web livreur, mobile — la recopieraient
   * chacun à sa façon.
   */
  editable: boolean;
  /**
   * L'admin peut DEMANDER le retour — chantier « Bon de retour livreur »,
   * point 2 : « Brouillon (admin seulement, tant qu'il n'a pas validé) ».
   *
   * Le brouillon du comptoir sort de l'ombre et devient une demande que le
   * livreur voit sur son téléphone. Faux sur un bon vide : un brouillon sans
   * colis ne demande rien (point 3).
   */
  requestable: boolean;
  /**
   * Le livreur peut prendre en charge : « je l'ai ».
   *
   * **Origine ADMIN uniquement** (point 2) : sur un bon qu'il a ouvert
   * lui-même, il avait déjà les colis en main au moment de cocher — se les
   * confier à soi-même n'attesterait rien.
   */
  pickable: boolean;
  /** Le livreur peut remettre à l'entrepôt : « je l'ai déposée ». */
  handoverable: boolean;
  /** L'admin peut confirmer la réception — c'est ce geste qui bouge les colis. */
  confirmable: boolean;
  /** L'admin peut clôturer. */
  closable: boolean;
  /** Annulable tant que la marchandise n'est pas reçue. */
  cancellable: boolean;
  /**
   * Le BROUILLON se supprime — chantier « Bon de retour livreur », point 3.
   *
   * Un bon vide n'est la preuve de rien : l'annuler laisserait au journal la
   * trace d'une demande qui n'a jamais existé, et la liste des bons annulés
   * finirait par ne plus vouloir rien dire. Seul le brouillon s'efface ; dès
   * qu'il est demandé, il a été VU par quelqu'un, et c'est l'annulation qui
   * s'applique.
   *
   * La portée décide de qui : l'admin sur les brouillons de son agence, le
   * livreur sur les siens — `handoverWhere(scope)` borne déjà la lecture, donc
   * la suppression aussi.
   */
  deletable: boolean;
}

/* --- Bon de retour au client --------------------------------------------- */

export interface ReturnSlipItem {
  id: Id;
  parcelId: Id;
  parcelCode: string;
  recipientName: string;
  destinationCityName: string;
  reason: ReturnReason;
  /**
   * Frais de retour GELÉS à la création de la ligne.
   *
   * Ils viennent des réglages métier (D-019), qui sont éditables : les
   * recalculer à l'affichage ferait changer un bon déjà remis le jour où
   * quelqu'un modifie un tarif — c'est exactement la dette n°17 des factures
   * legacy, qu'on ne rejoue pas ici.
   */
  feeApplied: AmountInCents;
  /** Statut COURANT du colis — l'accordéon du vendeur le montre. */
  statusId?: number;
  /** Date de création du COLIS : l'écran trie dessus, récent d'abord (chantier « Tri par défaut »). */
  parcelCreatedAt?: IsoDateTime;
  /** Nombre de cartons sous ce code (lot G, D-208) — affiché, jamais compté comme des colis. */
  quantity?: number;
}

export interface ReturnSlipListItem {
  id: Id;
  code: string;
  status: ReturnSlipStatus;
  clientId: Id;
  clientName: string;
  agencyId: Id;
  agencyName: string;
  parcelCount: number;
  totalFees: AmountInCents;
  deliveredAt: IsoDateTime | null;
  receivedByName: string | null;
  /**
   * Qui a remis le bon, côté transporteur. Clé d'AUTEUR (D-235) : un vendeur
   * la reçoit sous l'alias de l'équipe, jamais le nom de l'agent.
   */
  deliveredByName?: string | null;
  createdAt: IsoDateTime;
}

export interface ReturnSlipDetail extends ReturnSlipListItem {
  note: string | null;
  items: ReturnSlipItem[];
  /** Clôturable : encore en brouillon et porte au moins un colis. */
  closable: boolean;
}

/**
 * LA CRÉATION EN MASSE DES BONS DE RETOUR — lot E, E2 (D-202), lot J (D-213).
 *
 * La même forme pour l'aperçu (« N bons pour N clients, M colis », rien n'est
 * écrit) et pour le résultat (« 3 bons créés, 27 colis »). Un bon NEUF par
 * couple client × agence ; un brouillon déjà ouvert pour ce couple n'est ni
 * complété ni modifié (D-213, qui remplace D-202 sur ce point).
 */
export interface ReturnSlipBulkSummary {
  /** Bons neufs (aperçu : à créer). */
  created: number;
  /** Clients distincts concernés. */
  clients: number;
  /** Colis posés sur les bons. */
  parcels: number;
  /** Codes des bons créés — vide dans l'aperçu. */
  codes: string[];
}
