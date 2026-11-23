import type { NoteKind, NoteStatus } from '../enums';
import type { AmountInCents, Id, IsoDateTime } from './common';

/**
 * DOMAIN.md §1.12 — `bon_livraisons`, discriminé par `type`.
 * `pickup` = bon de ramassage (enlèvement chez le client),
 * `dropoff` = bon de dépôt (le client apporte les colis en agence).
 */
export interface DeliveryNote {
  id: Id;
  code: string;
  kind: NoteKind;
  status: NoteStatus;
  clientId: Id;
  agencyId: Id;
  locationCityId: Id;
  parcelIds: Id[];
  createdAt: IsoDateTime;
  validatedAt: IsoDateTime | null;
}

/** KPI de tête de liste des bons — même raison que `RouteStats` (C7 bloc 4). */
export interface DeliveryNoteStats {
  total: number;
  awaiting: number;
  parcels: number;
  /** Bons dont le ramassage est offert (DOMAIN.md §3.4). */
  waived: number;
  /**
   * La règle du ramassage, lue dans les réglages (D-019) — lot Argent 2, A2-5 :
   * offert à partir de `freePickupThreshold` colis, sinon `pickupFee` PAR BON.
   * L'aide du vendeur la cite telle qu'elle est réglée.
   */
  pickupFee: AmountInCents;
  freePickupThreshold: number;
}

export interface DeliveryNoteListItem extends DeliveryNote {
  clientName: string;
  cityName: string;
  /** Les colis ENCORE sur le bon. */
  parcelCount: number;
  /**
   * Recette 5, points 7 et 8 — « ramassés X/Y ». `pickedCount` : colis du bon
   * ramassés (par le livreur ou validés) ; `removedCount` : retirés, non remis
   * au ramassage. Le total Y vaut `parcelCount + removedCount`.
   */
  pickedCount: number;
  removedCount: number;
  /** DOMAIN.md §3.4 — ramassage offert au-delà de N colis. */
  pickupWaived: boolean;
}

/** Ligne de colis affichée dans un bon. */
export interface NoteParcel {
  id: Id;
  code: string;
  recipientName: string;
  destinationCityName: string;
  destinationDistrictName: string | null;
  description: string;
  statusId: number;
  codAmount: AmountInCents;
  deliveryFee: AmountInCents;
  /**
   * La date de création du COLIS — chantier « Tri par défaut » (11/11/2026).
   * L'écran range les lignes du plus récent au plus ancien ; le PDF, qui lit
   * le même détail, garde l'ordre du serveur.
   */
  parcelCreatedAt?: IsoDateTime;
  /** Nombre de cartons sous ce code (lot G, D-208) — affiché, jamais compté comme des colis. */
  quantity?: number;
  /**
   * CLIENT CORRIGÉ DEPUIS — D-256. Un bon validé ne se réécrit pas : son colis
   * passé depuis à un autre client y reste, marqué. Lu, pas stocké : le client
   * du colis n'est plus celui du bon.
   */
  clientCorrected?: boolean;
  /** Le client actuel du colis — pour l'agence seulement (`null` pour un vendeur). */
  correctedClientName?: string | null;
  /**
   * D-257 : le dernier changement de client du colis, lu au journal des
   * explications — sur TOUT bon qui le porte (actif ou validé, ancien ou
   * nouveau client).
   */
  clientCorrection?: NoteParcelClientCorrection | null;
}

/**
 * UN COLIS RETIRÉ DU BON, non remis au ramassage — Recette 5, point 7. Masqué
 * par défaut sur la fiche (« Voir les 50 non ramassés ») ; l'admin peut le
 * restaurer tant que `restoredAt` est vide.
 */
export interface RemovedNoteParcel {
  id: Id;
  code: string;
  recipientName: string;
  statusId: number;
  removedAt: IsoDateTime;
  removedByName: string | null;
  restoredAt: IsoDateTime | null;
  /** Pourquoi il a quitté le bon — le motif écrit au journal. */
  reason: string;
  /**
   * Restaurable : jamais une fois le bon validé ou le circuit clôturé
   * (recette 6, point 3, décision d'Anass). La liste reste lisible.
   */
  restorable: boolean;
}

export interface DeliveryNoteDetail extends DeliveryNoteListItem {
  parcels: NoteParcel[];
  removedParcels: RemovedNoteParcel[];
  /**
   * Bon déjà VALIDÉ dont un circuit ouvert porte encore les colis : l'agence
   * peut en retirer ce qui n'a jamais été remis (Recette 5, point 7 bis).
   */
  correctable: boolean;
  /** Le bon peut être validé : il est encore en attente et porte au moins un colis. */
  validatable: boolean;
  /**
   * Le frais de ramassage FACTURÉ pour ce bon (ligne hors colis, A2-5), HT ;
   * `null` s'il n'y en a pas (offert, dépôt, bon validé avant la règle).
   */
  pickupFeeCharged: AmountInCents | null;
  /** La règle réglée (D-019), citée par l'aide de validation : frais par bon, seuil d'exonération. */
  pickupRule: { fee: AmountInCents; freeFrom: number };
  /**
   * LES COLIS SORTIS DU BON par l'administration ou le vendeur — D-257 : client
   * changé, colis supprimé. Lisibles, jamais comptés dans les totaux (le bon
   * ne les porte plus). Les non remis gardent leur liste (`removedParcels`).
   */
  departedParcels?: DepartedNoteParcel[];
}

/** Un colis sorti d'un bon par un geste journalisé (D-257). */
export interface DepartedNoteParcel {
  code: string;
  recipientName: string;
  /** `client-change` : passé à un autre client ; `deleted` : supprimé. */
  kind: 'client-change' | 'deleted';
  at: IsoDateTime;
  /** L'auteur du geste ; `null` pour un vendeur qui lit son bon. */
  byName: string | null;
  reason: string | null;
}

/** « Client corrigé le … par …, avant : … » sur la ligne d'un bon (D-257). */
export interface NoteParcelClientCorrection {
  at: IsoDateTime;
  /** L'auteur ; `null` pour un vendeur (portée stricte). */
  byName: string | null;
  /** Le client d'avant ; `null` pour un vendeur qui n'était pas ce client. */
  fromClientName: string | null;
  /** Le motif, au survol ; `null` pour un vendeur. */
  reason: string | null;
}
