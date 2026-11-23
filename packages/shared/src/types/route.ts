import type { NoteStatus, RouteKind, RouteStatus } from '../enums';
import type { AmountInCents, Id, IsoDateTime } from './common';

/** DOMAIN.md §1.13 — `boxes` filtré sur `type='circuit'`. Le type `boxe` est mort (1 ligne). */
export interface Route {
  id: Id;
  code: string;
  status: RouteStatus;
  statusAt: IsoDateTime;
  kind: RouteKind;
  cityId: Id;
  originCityId: Id;
  courierId: Id;
  parcelIds: Id[];
  exitDate: IsoDateTime | null;
  createdAt: IsoDateTime;
}

/**
 * KPI de tête de liste des circuits — C7 bloc 4.
 *
 * Calculés par l'API sur TOUTE la sélection, pas sur la page affichée. Les
 * déduire des lignes chargées était tenable tant que l'écran les chargeait
 * toutes ; passer la liste en pagination serveur (B-5) aurait sinon transformé
 * « 412 circuits » en « 25 circuits ».
 */
export interface RouteStats {
  total: number;
  /** Circuits qui ne sont pas clôturés. */
  open: number;
  parcels: number;
  confirmed: number;
}

export interface RouteListItem extends Route {
  courierName: string;
  cityName: string;
  parcelCount: number;
  /** Colis confirmés par le livreur (pivot legacy `boxe_colis.confirmed`). */
  confirmedCount: number;
  /**
   * Colis retirés des bons du circuit, non remis au ramassage — Recette 5,
   * point 7. « 62/199 ramassés » : `confirmedCount` sur
   * `parcelCount + removedCount`.
   */
  removedCount: number;
}

/**
 * Ligne de colis affichée dans un circuit — jointures résolues côté API.
 *
 * **Les deux bouts du trajet voyagent, toujours** — C12 bloc 4. Le
 * destinataire ET l'expéditeur : c'est l'écran qui choisit lequel montrer,
 * d'après le type du circuit (`routeTargetsSender()`). Une tournée de
 * ramassage va chez l'expéditeur ; lui afficher le destinataire envoyait le
 * livreur à l'autre bout de la ville.
 *
 * Servir les deux plutôt qu'un champ « la personne à voir » calculé au
 * serveur : la feuille de route papier imprime l'un, l'écran l'autre, et un
 * champ unique aurait obligé à redemander la ligne pour changer de point de
 * vue.
 */
export interface RouteParcel {
  id: Id;
  code: string;
  /** La référence du vendeur — cherchée sur la fiche du circuit (chantier « Affichage », point 2). */
  externalCode?: string | null;
  recipientName: string;
  recipientPhone: string;
  destinationCityName: string;
  destinationDistrictName: string | null;
  deliveryAddress: string;
  /** L'EXPÉDITEUR : le nom de l'e-commerçant, tel que sa fiche le porte. */
  senderName: string;
  senderPhone: string;
  /** Adresse d'ENLÈVEMENT — celle où va un circuit de ramassage ou de retour. */
  pickupAddress: string;
  originCityName: string;
  statusId: number;
  /** Montant réellement à encaisser au pas de porte — DOMAIN.md §3.8. */
  codToCollect: AmountInCents;
  /** Pivot legacy `boxe_colis.confirmed` : le livreur a vu le colis en main. */
  confirmed: boolean;
  /** Montant réellement encaissé, figé à la confirmation en « livré ». */
  codCollected: AmountInCents | null;
  /** Commentaire de tournée — « portail fermé », « rappeler après 18 h ». */
  note: string | null;
  /** Date de création du COLIS : l'écran trie dessus, récent d'abord (chantier « Tri par défaut »). */
  parcelCreatedAt?: IsoDateTime;
  /** Nombre de cartons sous ce code (lot G, D-208) — affiché, jamais compté comme des colis. */
  quantity?: number;
  /**
   * « CORRIGÉ DEPUIS » — D-250 : le statut du colis a été corrigé par
   * l'administrateur après la clôture de ce circuit. La ligne n'est pas réécrite.
   */
  correctedAt?: IsoDateTime | null;
  correctedStatusId?: number | null;
}

/**
 * UN BON EMBARQUÉ DANS LE CIRCUIT — retouches Anass, point 5.
 *
 * « Sur chaque bon du circuit : nom du vendeur visible ; validation au choix —
 * par scan ou manuellement. »
 *
 * C'est la poignée que le livreur manipule sur le terrain : un vendeur, une
 * adresse, un nombre de colis. Cocher quarante lignes pour attester d'un
 * passage chez un seul vendeur n'est pas un geste de trottoir.
 */
export interface RouteNote {
  id: Id;
  code: string;
  status: NoteStatus;
  /** LE NOM DU VENDEUR — c'est chez lui que le livreur se rend. */
  clientName: string;
  clientPhone: string;
  pickupAddress: string;
  cityName: string;
  parcelCount: number;
  /** Retirés du bon, non remis au ramassage (Recette 5, point 7). */
  removedCount: number;
  /** Les colis de CE bon : le contrôle du scan s'y réfère. */
  parcelIds: Id[];
  /** Le livreur a attesté avoir la marchandise en main. */
  pickedUpAt: IsoDateTime | null;
}

export interface RouteDetail extends RouteListItem {
  parcels: RouteParcel[];
  /**
   * Les bons que le circuit emporte — vide sur une tournée de livraison ou de
   * retour, qui n'en portent pas. Servi toujours plutôt qu'optionnel : une
   * forme de réponse par type de circuit finit par diverger.
   */
  notes: RouteNote[];
  /**
   * Le circuit peut être clôturé. Livraison : chaque colis a son résultat.
   * Ramassage : rien ne bloque, au moins un colis ramassé — les non ramassés
   * seront retirés de leur bon (D-145).
   */
  closable: boolean;
  /**
   * Les colis que la clôture regarde — D-145. Sur un RAMASSAGE ouvert : les non
   * ramassés, qui seront retirés de leur bon. Sur une LIVRAISON ouverte : ceux
   * qui n'ont pas de résultat et bloquent la clôture. Vide sinon.
   */
  pendingParcels: Array<{ id: Id; code: string; noteCode: string | null }>;
  /** Ramassage : les colis RAMASSÉS selon la règle de clôture (« 5 sur 7 », D-145). */
  closurePicked: number;
  /** Livraison (D-155) : livrés, encore à retourner, bons de retour livreur en attente de réception. */
  deliveredCount: number;
  toReturnCount: number;
  pendingHandoverCount: number;
  /** Il est encore en brouillon : on peut y ajouter ou en retirer des colis. */
  editable: boolean;
  /** Il peut partir : brouillon, et il porte au moins un colis. */
  dispatchable: boolean;
  /** Total encaissé par le livreur sur la tournée. */
  codCollectedTotal: AmountInCents;
}

/**
 * Colis ÉLIGIBLE à un circuit, tel que l'écran de composition le propose.
 *
 * L'éligibilité est calculée par le serveur (ville, statut, absence d'un autre
 * circuit ouvert) : la lui redemander côté navigateur reviendrait à filtrer une
 * liste qu'on n'a pas.
 */
export interface RouteCandidate {
  id: Id;
  code: string;
  recipientName: string;
  recipientPhone: string;
  destinationDistrictId: Id | null;
  destinationDistrictName: string | null;
  deliveryAddress: string;
  statusId: number;
  codToCollect: AmountInCents;
  clientName: string;
  /** L'EXPÉDITEUR — C12 bloc 4. `clientName` en porte déjà le nom. */
  senderPhone: string;
  pickupAddress: string;
  originCityName: string;
  /**
   * LA VILLE QUI COMPTE pour ce circuit — lot F, F2 (D-206) : destination en
   * livraison, origine en ramassage et en retour. Le filtre Ville de la modale
   * « Ajouter des colis » lit ces deux champs.
   */
  cityId: Id;
  cityName: string;
  /**
   * OÙ IL EST DÉJÀ — Recette 5, point 6 (règle précisée par Anass), circuits
   * de ramassage seulement. `null` : libre. Sinon le bon actif et / ou le
   * circuit ouvert qui le portent ; `locked` : ce circuit est PARTI (dispatché
   * ou plus) — le colis est grisé, l'API refuse (422). Non verrouillé, l'ajout
   * le DÉPLACE.
   */
  heldBy: { noteCode: string | null; routeCode: string | null; locked: boolean } | null;
}

/**
 * BON ÉLIGIBLE à un circuit de ramassage — retouches Anass, point 5.
 *
 * « Lister les bons de ramassage ouverts non validés de la ville (code, nom du
 * vendeur, nb colis), plus les colis. »
 *
 * Les colis restent proposés à côté : un colis saisi hors bon — par l'agence,
 * au téléphone — doit pouvoir entrer dans la tournée sans qu'on lui invente un
 * bon pour la forme.
 */
export interface RouteNoteCandidate {
  id: Id;
  code: string;
  clientName: string;
  clientPhone: string;
  pickupAddress: string;
  cityName: string;
  parcelCount: number;
  createdAt: IsoDateTime;
}
