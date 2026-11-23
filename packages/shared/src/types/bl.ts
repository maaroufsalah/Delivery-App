import type { BlNoteStatus, DeliverySlipStatus } from '../enums';
import type { Id, IsoDateTime } from './common';

/**
 * LE BL — chantier « Retours, BL Clients Pro », point 5.
 *
 * « Bon de livraison à récupérer » : le papier que le destinataire signe et que
 * certains e-commerçants exigent en retour. **C'est un objet lié au colis, pas
 * un statut colis**, et toute la mécanique en découle :
 *
 *  - un colis peut être livré sans que son BL soit revenu ;
 *  - un BL peut voyager trois semaines après la livraison ;
 *  - les coller au même compteur rendrait « livré » ambigu — exactement la
 *    dette que `colis.statut_id` porte dans le legacy, où le transport et la
 *    facturation partageaient une colonne.
 *
 * **Rien de tout cela n'existe pour un client qui n'est pas Pro** (D-099) :
 * l'API refuse, et l'interface ne pose même pas la question.
 */

/** Une marche du parcours d'un BL — « date, heure, utilisateur ». */
export interface DeliverySlipStep {
  id: Id;
  status: DeliverySlipStatus;
  comment: string | null;
  authorName: string | null;
  createdAt: IsoDateTime;
}

export interface DeliverySlipListItem {
  id: Id;
  status: DeliverySlipStatus;
  parcelId: Id;
  parcelCode: string;
  recipientName: string;
  clientId: Id;
  clientName: string;
  courierId: Id | null;
  courierName: string | null;
  /** « Référence saisie facultative » — le numéro que le vendeur reconnaîtra. */
  reference: string | null;
  /**
   * La date de LIVRAISON du colis — lot E, E1.2 : le dernier passage du colis
   * en « Livré ». `null` pour un BL d'avant le lot E posé sur un colis qui ne
   * l'a jamais été.
   */
  deliveredAt: IsoDateTime | null;
  collectedAt: IsoDateTime | null;
  handedOverAt: IsoDateTime | null;
  receivedAt: IsoDateTime | null;
  returnedAt: IsoDateTime | null;
  /** Qui a signé chez le vendeur — lot F, F4. */
  returnedToName: string | null;
  closedAt: IsoDateTime | null;
  createdAt: IsoDateTime;
}

/**
 * LE SUIVI DU BL SUR LA FICHE COLIS — point 5, dernier alinéa.
 *
 * `requested` dit que le colis PORTE la demande (`parcel.blRequested`), et
 * c'est vrai même quand aucun papier n'existe encore : c'est la première marche
 * de la frise, « BL à récupérer ». `slip` est `null` tant que le livreur ne l'a
 * pas rapporté — le colis demande, le BL répond.
 */
export interface ParcelBlTracking {
  requested: boolean;
  slip: (DeliverySlipListItem & { steps: DeliverySlipStep[] }) | null;
}

/* --- Bon de retour BL — code `BRB-` --------------------------------------- */

export interface BlNoteStep {
  id: Id;
  status: BlNoteStatus;
  comment: string | null;
  authorName: string | null;
  createdAt: IsoDateTime;
}

export interface BlNoteListItem {
  id: Id;
  code: string;
  status: BlNoteStatus;
  courierId: Id;
  courierName: string;
  /**
   * LE client pro — singulier, et c'est voulu.
   *
   * C'est à lui qu'on rendra la liasse ; mélanger deux vendeurs dans un même
   * bon obligerait à la trier au comptoir, devant celui qui attend.
   */
  clientId: Id;
  clientName: string;
  agencyId: Id;
  agencyName: string;
  slipCount: number;
  createdByName: string | null;
  handedOverAt: IsoDateTime | null;
  receivedAt: IsoDateTime | null;
  returnedAt: IsoDateTime | null;
  returnedToName: string | null;
  closedAt: IsoDateTime | null;
  createdAt: IsoDateTime;
}

export interface BlNoteDetail extends BlNoteListItem {
  note: string | null;
  /** Les BL du bon, avec leur colis associé. */
  items: DeliverySlipListItem[];
  steps: BlNoteStep[];
  /**
   * CE QUE L'ÉCRAN A LE DROIT DE PROPOSER, calculé par le serveur.
   *
   * Même règle que le bon de retour colis : trois espaces qui recopieraient la
   * machine à états en feraient trois versions.
   */
  editable: boolean;
  /** Le livreur peut remettre la liasse à l'entrepôt. */
  handoverable: boolean;
  /** « Confirmer la réception » — l'admin a les papiers en main. */
  receivable: boolean;
  /** « Confirmer la remise au vendeur » — le vendeur a signé. */
  returnable: boolean;
  /** Clôturer : l'affaire est classée. */
  closable: boolean;
}
