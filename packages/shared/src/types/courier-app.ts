import type { SettlementMode } from '../courier-pay';
import type { NoteKind, NoteStatus, PayModel, RouteKind, VehicleType } from '../enums';
import type { AmountInCents, Id, IsoDateTime } from './common';

/**
 * Ce que l'application du LIVREUR consomme — M1, docs/DECISIONS.md D-072.
 *
 * Ces contrats sont volontairement **plus pauvres** que ceux du back-office.
 * Un livreur n'a besoin ni des frais facturés à l'e-commerçant, ni du nom de
 * celui-ci, ni de l'état de facturation du colis : il lui faut de quoi trouver
 * une adresse, appeler quelqu'un, encaisser le bon montant et rapporter une
 * issue. Tout le reste est de la donnée commerciale qui n'a pas à voyager sur
 * un téléphone qui se perd.
 */

/** Le livreur, tel qu'il se voit — profil, rattachement, régime de paie. */
export interface CourierMe {
  id: Id;
  firstName: string;
  lastName: string;
  phone: string;
  vehicleType: VehicleType;

  /** Sa ville d'exercice : c'est elle que le scan donne au circuit (D-073). */
  cityId: Id;
  cityName: string;
  agencyId: Id | null;
  agencyName: string | null;

  /**
   * Les DEUX axes de sa paie — C11 bloc 1, D-058.
   *
   * Ils voyagent parce que l'application les AFFICHE : un livreur
   * `at-remittance` doit lire « commission déduite à la remise » sur son
   * relevé, sinon un net à zéro ressemble à une erreur. Ils ne décident de
   * rien côté application ; le calcul reste au serveur (D-065).
   */
  payModel: PayModel;
  settlementMode: SettlementMode;
}

/** Un colis DANS une tournée, vu du terrain. */
export interface CourierRouteParcel {
  id: Id;
  code: string;
  /**
   * La RÉFÉRENCE COMMANDE du vendeur — point 7 du 26/09. Le livreur la lit sur
   * l'étiquette du vendeur, la scanne, et la voit sur ses écrans.
   */
  externalCode?: string | null;
  /** Rang dans la tournée — l'ordre du sac (D-073). */
  sequence: number;

  recipientName: string;
  recipientPhone: string;
  deliveryAddress: string;
  destinationCityName: string;
  destinationDistrictName: string | null;

  /**
   * L'EXPÉDITEUR — C12 bloc 4, et une RÉVISION assumée de D-072.
   *
   * D-072 dit que l'application du livreur ne porte pas le nom de
   * l'e-commerçant : c'est de la donnée commerciale, et elle n'a pas à voyager
   * sur un téléphone qui se perd. La règle valait pour une tournée de
   * LIVRAISON, où le livreur n'a aucun besoin de savoir qui expédie.
   *
   * Elle ne peut pas valoir pour un RAMASSAGE : le livreur va chercher la
   * marchandise chez ce commerçant-là, il lui faut son nom, son adresse et son
   * téléphone, sinon il n'a personne à demander en arrivant. Le champ voyage
   * donc pour les deux types, et l'écran n'en montre qu'un — celui du
   * `routeTargetsSender()` de la tournée.
   */
  senderName: string;
  senderPhone: string;
  /** Adresse d'ENLÈVEMENT. C'est là que va un circuit de ramassage. */
  pickupAddress: string;
  originCityName: string;

  statusId: number;
  /**
   * Ce qu'il faut réellement encaisser au pas de porte — DOMAIN.md §3.8.
   *
   * **Pas `codAmount`.** Le legacy affichait le prix du colis et les livreurs
   * encaissaient le mauvais montant quand les frais n'étaient pas inclus. Le
   * calcul est fait au serveur, une fois, et l'application n'a qu'à l'afficher.
   */
  codToCollect: AmountInCents;
  /** Lot G (D-208) : le nombre de cartons — « × 4 cartons » sur la carte. */
  quantity?: number;
  /**
   * Ce que cette course rapporte au livreur, si elle est déjà figée (D-065).
   *
   * `null` tant qu'il n'a rien confirmé : la commission se fige à l'issue de
   * la course, et annoncer un montant avant de l'avoir gagné serait une
   * promesse que l'échec démentirait.
   */
  courierCommission: AmountInCents | null;

  /** Le livreur a déjà rapporté une issue pour ce colis sur cette tournée. */
  confirmed: boolean;
  /** Coordonnées, quand le colis en porte. Pour le bouton « itinéraire ». */
  latitude: number | null;
  longitude: number | null;
}

/**
 * UN COLIS À LIVRER, dans la liste unique du menu Livraison — chantier « Menu
 * Livraison : liste unique » (demande de Badr, SpeedyTrans).
 *
 * La ligne d'un circuit, plus le circuit qui la porte : le livreur ne passe
 * plus par le circuit, mais il en lit le code et peut filtrer dessus.
 */
export interface CourierDeliveryParcel extends CourierRouteParcel {
  routeCode: string;
}

/**
 * `GET /courier/me/deliveries` — tous les colis À LIVRER du livreur, tous
 * circuits de livraison PARTIS confondus.
 *
 * Y figure un colis qui roule (13) ou qui porte une issue de terrain autre que
 * « Livré », et qu'aucun bon de retour `RL-` actif ne réserve. Un colis livré
 * en sort (règle D), un colis posé sur un bon aussi (règle E).
 *
 * `deliveredCount` / `totalCount` : « X livrés / Y », sur ces mêmes circuits.
 * Un circuit qui se clôt emporte ses colis hors du compteur.
 */
export interface CourierDeliveries {
  deliveredCount: number;
  totalCount: number;
  parcels: CourierDeliveryParcel[];
}

/** Une tournée du livreur — brouillon en cours de scan, ou tournée partie. */
/**
 * UN BON DANS LA TOURNÉE DU LIVREUR — retouches Anass, point 5.
 *
 * « Sur chaque bon du circuit : nom du vendeur visible ; validation au choix —
 * par scan ou manuellement. »
 *
 * C'est la poignée du terrain. Un vendeur qui prépare quarante colis fait UN
 * bon : le livreur voit un nom, une adresse, un nombre — et coche une fois, pas
 * quarante. `parcelIds` voyage parce que c'est ce qui permet au scan de refuser
 * un carton qui n'est pas de ce bon-là, tout de suite, devant la personne.
 */
export interface CourierRouteNote {
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
  parcelIds: Id[];
  pickedUpAt: IsoDateTime | null;
}

export interface CourierRoute {
  id: Id;
  code: string;
  /**
   * Le TYPE de la tournée — C12 bloc 3.
   *
   * Il décide de tout ce que l'écran montre : la personne à appeler, l'adresse
   * de l'itinéraire, l'issue mise en avant. Sans lui, l'application affichait
   * un destinataire sur une tournée de ramassage.
   */
  kind: RouteKind;
  /**
   * `draft` : en cours de composition au scan. `active` : partie. `closed` :
   * terminée — l'onglet HISTORIQUE du chantier « Retours, BL Clients Pro »
   * (point 3), en lecture seule.
   */
  status: 'draft' | 'active' | 'closed';
  cityId: Id;
  cityName: string;
  createdAt: IsoDateTime;
  parcelCount: number;
  /** Colis restants à traiter : le seul chiffre qu'un livreur regarde. */
  remainingCount: number;
  /** Livraison (D-155) : livrés, encore à retourner, bons de retour en attente de réception. */
  deliveredCount: number;
  toReturnCount: number;
  pendingHandoverCount: number;
  /** Somme des `codToCollect` encore à encaisser. */
  codToCollect: AmountInCents;
  parcels: CourierRouteParcel[];
  /**
   * Les BONS de la tournée — retouches Anass, point 5. Vide sur une livraison :
   * seul le ramassage en porte. Servi toujours plutôt qu'optionnel, pour la
   * même raison que l'expéditeur sur `CourierRouteParcel` — deux formes de
   * réponse selon le type finissent par diverger.
   */
  notes: CourierRouteNote[];
}

/* --- Le scan -------------------------------------------------------------- */

/**
 * Les refus du scan, en MOTIFS machine — M1, D-073.
 *
 * L'application les traduit ; le message du serveur reste le secours. La
 * distinction compte sur le terrain : « déjà chez Youssef » se règle en
 * appelant Youssef, « mauvaise ville » en reposant le colis, et les deux ne
 * demandent pas le même geste.
 */
export const SCAN_REFUSALS = [
  'parcel-unknown',
  'parcel-other-route',
  'parcel-wrong-city',
  'parcel-bad-status',
  'parcel-already-scanned',
  'route-dispatched',
  'scan-disabled',
] as const;

export type ScanRefusal = (typeof SCAN_REFUSALS)[number];

/** Ce que rend un scan accepté : la tournée entière, et le colis ajouté. */
export interface ScanResult {
  route: CourierRoute;
  /** Le colis qui vient d'entrer. L'application le fait clignoter en tête. */
  added: CourierRouteParcel;
  /** Vrai quand ce scan a OUVERT la tournée du jour. */
  routeCreated: boolean;
  /** Lot I, I1 (D-211) : vrai quand ce scan a PRIS le colis à un autre livreur. */
  transferred?: boolean;
  /** Lot L (D-219) : la ville vers laquelle le colis pris a changé de destination. */
  reroutedTo?: string;
}

/* --- Ce que L2 ajoute : l'accueil, les ramassages, les chèques ----------- */

/**
 * LES HUIT CHIFFRES DE L'ACCUEIL — spec d'Anass §1, maquette 2.
 *
 * Six se déduisaient déjà de la tournée active (M4, `dayStats`). Deux ne le
 * peuvent pas : « à remettre » vit sur les REMISES — un livreur qui n'a rien
 * livré aujourd'hui peut devoir trois mille dirhams d'hier — et le taux de
 * livraison se mesure sur une PÉRIODE que la tournée du jour ne connaît pas.
 *
 * Les huit voyagent ensemble pour que le web et le téléphone lisent le même
 * chiffre : deux sources donneraient deux comptes de la même journée.
 */
export interface CourierPerformance {
  period: 'day' | 'month';
  toPickup: number;
  toDeliver: number;
  delivered: number;
  returning: number;
  failed: number;
  collectedInCents: AmountInCents;
  /** Encaissé et pas encore versé à l'agence — la dette du livreur. */
  toRemitInCents: AmountInCents;
  /**
   * Le dénominateur est ce qui a reçu une ISSUE sur la période, pas ce qui a
   * été confié : un colis encore en tournée n'est ni un succès ni un échec, et
   * le compter ferait baisser le taux d'un livreur qui n'a pas fini sa journée.
   */
  deliveryRate: {
    delivered: number;
    total: number;
    percent: number;
  };
}

/**
 * Un bon de RAMASSAGE ou de DÉPÔT, vu du livreur — spec §4 et §5C.
 *
 * C'est le `DeliveryNote` du domaine (DOMAIN.md §1.12), discriminé par son
 * type. Le livreur y est relié par ses COLIS : un bon le concerne si l'un de
 * ses colis lui est affecté — un bon ne porte pas de `courierId`.
 */
export interface CourierPickupNote {
  id: Id;
  code: string;
  kind: NoteKind;
  status: 'awaiting-validation' | 'validated';
  clientName: string;
  clientPhone: string;
  address: string;
  cityName: string;
  /**
   * SES colis sur ce bon, pas tous ceux du bon.
   *
   * Un bon de ramassage peut porter les colis de plusieurs livreurs. Afficher
   * le total ferait lire « 12 colis » à quelqu'un qui n'en prend que trois, et
   * le ferait attendre neuf colis qui ne sont pas pour lui.
   */
  parcelCount: number;
  /**
   * SES colis retirés du bon, non remis au ramassage — Recette 5, points 7 et
   * 8 : « 150/200 ramassés » compte aussi ce qui n'a pas été remis.
   */
  removedCount: number;
  /**
   * Ceux de SES colis qui ont quitté l'attente de ramassage.
   *
   * C'est ce qui donne le troisième onglet de la maquette 7. Le bon n'a que
   * deux états en base — à valider, validé (DOMAIN.md §1.12) — et Anass en
   * demande trois : En attente, En cours, Terminés. « En cours » n'est pas un
   * état du bon, c'est un état du TRAVAIL : le livreur en a pris une partie.
   * Le déduire des colis évite d'ajouter un statut en base pour une nuance
   * d'affichage (aucune migration).
   */
  pickedCount: number;
  createdAt: IsoDateTime;
  validatedAt: IsoDateTime | null;
}

/**
 * Un chèque que le livreur a reçu et doit rendre — spec §5B, maquette 9.
 *
 * La source est le RELEVÉ de rémunération (C11, D-067), pas une table de
 * chèques : un chèque reçu par un livreur EST une ligne de son relevé, avec
 * son numéro, sa banque et sa date. Une table à part aurait fait deux vérités
 * sur le même papier.
 */
export interface CourierCheque {
  id: Id;
  statementCode: string;
  number: string;
  bank: string | null;
  issuedOn: IsoDateTime | null;
  amountInCents: AmountInCents;
  /** « Remis » veut dire RÉGLÉ : tant que le relevé n'est pas soldé, le chèque circule. */
  handedOver: boolean;
  hasPhoto: boolean;
}
