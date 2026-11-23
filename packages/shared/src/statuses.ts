import type {
  BlNoteStatus,
  DeliverySlipStatus,
  NoteStatus,
  ReturnHandoverStatus,
  RouteStatus,
} from './enums';

/**
 * Référentiel des statuts colis.
 *
 * Source : DOMAIN.md §2.1 — 38 statuts legacy, dont 4 jamais écrits en base
 * (15 « En agence destination », 18 « Ramassé par le livreur », 30 « Au guichet
 * Amana », 36 « 3e appel + SMS ») retirés du référentiel v2 → 34 statuts.
 *
 * **18 est revenu** (retouches Anass, point 5) : il manquait un cran entre « le
 * livreur est passé » et « la marchandise est à l'entrepôt ». → 35 statuts.
 *
 * Les `id` sont ceux du legacy : ils sont conservés tels quels pour que la
 * reprise de données et l'API partenaires restent compatibles. Ils ne sont
 * donc PAS contigus.
 *
 * `color` : le legacy stocke un triplet RGB en texte (« 211, 84, 0 »),
 * converti ici en hexadécimal (DOMAIN.md §4 dette n°15).
 * `icon`  : nom d'icône lucide-react. Le legacy portait des classes Flaticon
 * propriétaires du thème Metronic, dont deux contenaient un `\n` littéral
 * (ids 8 et 14) — remappées proprement.
 */

/** Familles de statuts, utilisées pour le regroupement et le filtrage UI. */
export const PARCEL_STATUS_GROUPS = [
  'initial',
  'transit',
  'pivot',
  'retry',
  'failure',
  'return',
  'terminal',
] as const;

export type ParcelStatusGroup = (typeof PARCEL_STATUS_GROUPS)[number];

interface ParcelStatusShape {
  /** Identifiant legacy, conservé pour la reprise de données. */
  id: number;
  /** Clé i18n, résolue sous `status.<key>`. */
  key: string;
  /** Couleur hexadécimale, convertie du triplet RGB legacy. */
  color: `#${string}`;
  /** Nom d'icône lucide-react. */
  icon: string;
  group: ParcelStatusGroup;
}

export const PARCEL_STATUSES = [
  { id: 1, key: 'awaiting-pickup', color: '#D35400', icon: 'Hourglass', group: 'initial' },
  { id: 2, key: 'awaiting-arrival', color: '#E67E22', icon: 'MapPin', group: 'initial' },
  { id: 3, key: 'picked-up', color: '#3498DB', icon: 'Package', group: 'pivot' },
  { id: 4, key: 'delivered', color: '#6AB04C', icon: 'UserCheck', group: 'terminal' },
  { id: 5, key: 'returned', color: '#D63031', icon: 'RotateCcw', group: 'terminal' },
  { id: 6, key: 'refused', color: '#ED4C67', icon: 'X', group: 'failure' },
  { id: 7, key: 'shipped', color: '#2F3640', icon: 'Truck', group: 'transit' },
  { id: 8, key: 'out-of-zone', color: '#2D3436', icon: 'MapPinOff', group: 'failure' },
  { id: 9, key: 'unreachable', color: '#D63031', icon: 'PhoneOff', group: 'failure' },
  { id: 10, key: 'wrong-number', color: '#576574', icon: 'TriangleAlert', group: 'failure' },
  { id: 11, key: 'no-answer-1', color: '#341F97', icon: 'Phone', group: 'retry' },
  { id: 12, key: 'shipping-in-progress', color: '#D63031', icon: 'RefreshCw', group: 'transit' },
  { id: 13, key: 'out-for-delivery', color: '#4B4B4B', icon: 'Navigation', group: 'pivot' },
  { id: 14, key: 'postponed', color: '#8C7AE6', icon: 'CalendarClock', group: 'retry' },
  { id: 16, key: 'awaiting-delivery', color: '#FFA801', icon: 'Inbox', group: 'pivot' },
  { id: 17, key: 'pickup-in-progress', color: '#4B4B4B', icon: 'PackageSearch', group: 'transit' },
  /*
   * 18 « Ramassé par le livreur » REVIENT au référentiel — retouches Anass,
   * point 5.
   *
   * Il en avait été retiré avec trois autres, au motif qu'aucune ligne de
   * production ne le portait (§2.1). C'était juste : le legacy ne s'en servait
   * pas. Le métier, lui, en avait besoin — il manquait un cran entre « le
   * livreur est passé » et « la marchandise est à l'entrepôt », et sans lui les
   * deux gestes n'en faisaient qu'un. Un colis comptait comme ramassé dès que
   * le livreur cochait une case dans la rue, avant que personne à l'agence ne
   * l'ait vu.
   *
   * Son `id` legacy est repris tel quel, comme tous les autres : en inventer un
   * neuf pour un état que le référentiel d'origine nommait déjà casserait la
   * reprise de données et l'API partenaires (D-001).
   *
   * Famille `pivot` avec `3 Colis ramassé` : c'est un point de bascule de la
   * marchandise, pas un transit.
   */
  { id: 18, key: 'picked-up-by-courier', color: '#2980B9', icon: 'PackageCheck', group: 'pivot' },
  { id: 19, key: 'cancelled-by-seller', color: '#E15F41', icon: 'Ban', group: 'failure' },
  { id: 20, key: 'awaiting-return', color: '#D63031', icon: 'Hourglass', group: 'return' },
  { id: 21, key: 'recovery-in-progress', color: '#D35400', icon: 'Hourglass', group: 'return' },
  { id: 22, key: 'recovered-by-courier', color: '#F97F51', icon: 'PackageCheck', group: 'return' },
  { id: 23, key: 'return-collected', color: '#F97F51', icon: 'PackageOpen', group: 'terminal' },
  { id: 24, key: 'return-in-progress', color: '#D35400', icon: 'Undo2', group: 'return' },
  { id: 25, key: 'voicemail', color: '#341F97', icon: 'Voicemail', group: 'retry' },
  { id: 26, key: 'customer-travelling', color: '#341F97', icon: 'Plane', group: 'retry' },
  { id: 27, key: 'cancelled-by-customer', color: '#341F97', icon: 'CircleOff', group: 'failure' },
  { id: 28, key: 'already-delivered', color: '#341F97', icon: 'PackageCheck', group: 'failure' },
  { id: 29, key: 'not-interested', color: '#341F97', icon: 'ThumbsDown', group: 'failure' },
  { id: 31, key: 'in-stock', color: '#341F97', icon: 'Warehouse', group: 'pivot' },
  { id: 32, key: 'no-answer-2', color: '#341F97', icon: 'Phone', group: 'retry' },
  { id: 33, key: 'no-answer-3', color: '#341F97', icon: 'Phone', group: 'retry' },
  { id: 34, key: 'no-answer-1-sms', color: '#341F97', icon: 'MessageSquare', group: 'retry' },
  { id: 35, key: 'no-answer-2-sms', color: '#341F97', icon: 'MessageSquare', group: 'retry' },
  { id: 37, key: 'wrong-destination', color: '#341F97', icon: 'MapPinX', group: 'failure' },
  { id: 38, key: 'scheduled', color: '#1E40AF', icon: 'CalendarCheck', group: 'initial' },
] as const satisfies readonly ParcelStatusShape[];

/**
 * Le type est dérivé de la donnée, pas déclaré au-dessus : `key` conserve son
 * union littérale, ce qui rend `t(status.key)` vérifiable par le typage strict
 * des messages next-intl.
 */
export type ParcelStatusDefinition = (typeof PARCEL_STATUSES)[number];
export type ParcelStatusId = ParcelStatusDefinition['id'];
export type ParcelStatusKey = ParcelStatusDefinition['key'];

const STATUS_BY_ID: ReadonlyMap<number, ParcelStatusDefinition> = new Map<
  number,
  ParcelStatusDefinition
>(PARCEL_STATUSES.map((status) => [status.id, status]));

/** Statut par id, ou `undefined` si l'id n'appartient pas au référentiel v2. */
export function findParcelStatus(id: number): ParcelStatusDefinition | undefined {
  return STATUS_BY_ID.get(id);
}

/**
 * LE VENDEUR PEUT-IL ENCORE TOUCHER À CE COLIS ? — recette SpeedyTrans 3,
 * point 3.
 *
 * Oui tant qu'il n'est pas ramassé : les statuts du groupe « initial »
 * (1 Attente de ramassage, 2 En attente d'arrivée). Dès le 18 « Ramassé par
 * le livreur », ou le 3 posé par la validation d'un BR-/BD-, la marchandise
 * est chez le transporteur : ni modification ni suppression. Écrit sur le
 * GROUPE et non sur deux numéros, pour qu'un statut initial ajouté demain
 * suive la règle. L'API et les écrans lisent la même fonction.
 */
export function isEditableBySeller(statusId: number): boolean {
  return findParcelStatus(statusId)?.group === 'initial';
}

/**
 * Statut par id. Lève si l'id est inconnu : à n'utiliser que sur des données
 * dont on garantit l'appartenance au référentiel (mocks, retours d'API validés).
 */
export function getParcelStatus(id: number): ParcelStatusDefinition {
  const status = STATUS_BY_ID.get(id);
  if (!status) throw new Error(`Statut colis inconnu : ${id}`);
  return status;
}

export function getStatusesByGroup(group: ParcelStatusGroup): ParcelStatusDefinition[] {
  return PARCEL_STATUSES.filter((status) => status.group === group);
}

/**
 * Statuts terminaux. DOMAIN.md §2.2 relève 39 transitions `4 → 6` en prod :
 * le legacy autorise d'en sortir. La v2 doit poser une machine à états — ce
 * drapeau en est le point d'ancrage côté UI.
 */
export function isTerminalStatus(id: number): boolean {
  return findParcelStatus(id)?.group === 'terminal';
}

export function isFailureStatus(id: number): boolean {
  const group = findParcelStatus(id)?.group;
  return group === 'failure' || group === 'retry';
}

/**
 * Les trois états du PARCOURS DE RETOUR, nommés.
 *
 * Aucun statut n'a été créé pour C3 : le référentiel v2 les portait déjà, avec
 * leurs `id` legacy, et en inventer de nouveaux aurait fait doublon — un même
 * état sous deux identifiants casse la reprise de données et l'API partenaires
 * (§2.1). Ce qui manquait, c'était le NOM : `20` ne dit pas ce qu'il signifie
 * au milieu d'un service.
 *
 * | Constante | id | Libellé legacy | Sens |
 * |---|---|---|---|
 * | `RETURN_PENDING` | 20 | En attente de retour | le colis est à retourner |
 * | `RETURNED_TO_AGENCY` | 22 | Récupéré par livreur | remis par le livreur, en agence |
 * | `RETURNED_TO_CLIENT` | 23 | Retour récupéré | rendu au client — **terminal** |
 *
 * `RETURNED_TO_CLIENT` figure déjà dans `LOCKED_STATUS_IDS` : il ne se quitte
 * que par une contestation.
 */
export const RETURN_PENDING = 20;
export const RETURNED_TO_AGENCY = 22;
export const RETURNED_TO_CLIENT = 23;

/**
 * Les trois statuts FACTURABLES, nommés — DOMAIN.md §3.9.
 *
 * Le legacy attachait à une facture les colis en statut `4, 5, 6`
 * (`FactureController.php:151`) : livré, retourné, refusé. Les deux derniers
 * ne rapportent rien à l'e-commerçant — ils lui COÛTENT les frais d'échec —
 * mais ils appartiennent bien à la facture : c'est là qu'on lui dit ce qu'il
 * doit.
 *
 * | Constante | id | Ce que la facture en fait |
 * |---|---|---|
 * | `DELIVERED` | 4 | COD encaissé moins les frais de livraison |
 * | `RETURNED` | 5 | rien d'encaissé, frais d'échec retenus |
 * | `REFUSED` | 6 | idem |
 *
 * **Point ouvert B-2** : un retour (5) est-il facturé au tarif d'échec ou au
 * tarif de livraison ? Le legacy traitait les deux pareil, ce qui ressemble à
 * un oubli. En attendant la réponse du PO, on applique le tarif d'échec — la
 * valeur vient d'un `Setting`, la corriger ne demandera pas de migration.
 */
/**
 * `2 En attente d'arrivée` — nommé depuis le chantier « Étiquette orange »,
 * point 2 : c'est le statut de naissance du colis d'ÉCHANGE.
 *
 * Le vendeur prépare le neuf ; tant qu'il n'est pas entre les mains du
 * transporteur, il attend. C'est le même état qu'un colis qu'un client vient
 * déposer au comptoir, et c'est exact : dans les deux cas la marchandise n'est
 * pas encore à nous.
 */
export const AWAITING_ARRIVAL = 2;

export const DELIVERED = 4;
export const RETURNED = 5;
export const REFUSED = 6;

export const BILLABLE_STATUS_IDS = [DELIVERED, RETURNED, REFUSED] as const;

export function isBillableStatus(id: number): boolean {
  return (BILLABLE_STATUS_IDS as readonly number[]).includes(id);
}

/**
 * UN COLIS EST FACTURABLE — CALC-02, D-167.
 *
 * Livré (4), Retourné (5), Refusé (6) : comme toujours. Et désormais un colis
 * du FLUX RETOUR (20 En attente de retour, 22 Retourné à l'agence, 23 Rendu au
 * client) qui porte un ÉCHEC : son frais d'échec, constaté au refus ou à
 * l'injoignable, est dû par le vendeur même après que le colis lui est revenu.
 * Avant, il n'était « jamais facturable » dès qu'il quittait le 6 — 10 DH
 * perdus par colis refusé inter-villes.
 */
export function isBillableParcel(parcel: { statusId: number; isFailed: boolean }): boolean {
  if (isBillableStatus(parcel.statusId)) return true;
  return parcel.isFailed && [20, 21, 22, 23, 24].includes(parcel.statusId);
}

/**
 * CE QU'UN RETOUR PEUT REPRENDRE — retouches Anass, point 7.
 *
 * « À la création [d'un bon de retour], proposer tous les colis du livreur /
 * de la ville dont le statut est différent de Ramassé, Ramassé par le livreur,
 * Livré et En cours de livraison. »
 *
 * La règle est écrite en EXCLUSION, et c'est le bon sens de lecture : un colis
 * rentre à l'agence quand il n'est PAS en train d'aller quelque part. Les
 * quatre exclus sont les quatre moments où il est ailleurs qu'en attente :
 *
 *  - `3` et `18` : il vient d'être ramassé, il part chez le destinataire ;
 *  - `13` : il est en tournée, à cette minute ;
 *  - `4` : il est arrivé, et l'affaire est close.
 *
 * Tout le reste — les échecs, les relances, les reports, l'attente de retour —
 * est du travail à rapporter, et la liste de composition doit le montrer.
 * Elle ne montrait que `20 En attente de retour` : un colis « injoignable »
 * depuis trois jours n'entrait dans aucun bon tant que personne n'avait
 * basculé son statut à la main.
 *
 * **Aucun statut nouveau** — la mission le dit, et c'est ce qui la rend
 * applicable : le bon repose sur des états qui existent déjà.
 */
export const RETURN_EXCLUDED_STATUS_IDS: readonly number[] = [3, 18, 4, 13];

/** Ce colis peut-il entrer dans un bon de retour ou une remise ? */
export function isReturnEligibleStatus(statusId: number): boolean {
  return !RETURN_EXCLUDED_STATUS_IDS.includes(statusId);
}

/**
 * CE QUE LE LIVREUR PEUT RAPPORTER — Recette 7, point 2 (D-153).
 *
 * La règle d'exclusion ci-dessus est celle de l'AGENCE : « tout ce qui traîne »
 * entre dans la liste des retours en attente, et un bon ouvert au comptoir peut
 * tout prendre. Le bon de retour DU LIVREUR (RL- d'origine COURIER) ne propose
 * que ce qu'il a EN MAIN et n'a pas pu livrer : « + Créer un bon de retour »
 * offrait des colis « En cours de ramassage », qui n'y ont rien à faire.
 *
 * Inclus — les échecs et relances de course, et le retour demandé par l'agence :
 *  - 6 refusé · 8 hors zone · 37 envoi erroné (adresse introuvable) ;
 *  - 9 injoignable · 10 faux numéro · 11, 32, 33 sans réponse · 34, 35 SMS ·
 *    25 messagerie · 26 client en voyage ;
 *  - 14 reporté · 27 annulé par le client · 28 déjà livré · 29 pas intéressé ·
 *    19 annulé par le vendeur après ramassage ;
 *  - 20 en attente de retour (l'agence l'a demandé).
 *
 * Exclus — avant le ramassage (1, 2, 17, 18, 38), 3 ramassé en attente de
 * circuit, 7 / 12 / 13 / 16 en acheminement ou en livraison, 4 livré, 31 en
 * stock, et tout ce qui est déjà rentré ou reparti (22, 23, 24, 5, 21).
 */
export const COURIER_RETURN_STATUS_IDS: readonly number[] = [
  6, 8, 9, 10, 11, 14, 19, 20, 25, 26, 27, 28, 29, 32, 33, 34, 35, 37,
];

/** Ce colis peut-il entrer dans le bon de retour du LIVREUR ? */
export function isCourierReturnStatus(statusId: number): boolean {
  return COURIER_RETURN_STATUS_IDS.includes(statusId);
}

/** Le parcours, dans l'ordre. Un retour ne saute aucune de ces marches. */
export const RETURN_FLOW_STATUS_IDS = [
  RETURN_PENDING,
  RETURNED_TO_AGENCY,
  RETURNED_TO_CLIENT,
] as const;

export function isReturnFlowStatus(id: number): boolean {
  return (RETURN_FLOW_STATUS_IDS as readonly number[]).includes(id);
}

/**
 * « MES RETOURS », CÔTÉ VENDEUR — lot V, V1 (D-234).
 *
 * La pile de l'agence (`RETURN_EXCLUDED_STATUS_IDS`) est une liste de TRAVAIL :
 * tout ce qui traîne, y compris un colis en attente de ramassage. Le vendeur,
 * lui, demande « qu'est-ce qui me revient ? » : les marches du retour (20, 21,
 * 22, 24, en route vers lui) et le retour accompli (23 rendu, 5 retourné). Le
 * serveur y ajoute les RET- d'échange et ce que la règle des BRT- tient pour
 * rentré en agence ; il n'y garde jamais un colis en route vers le destinataire
 * ou livré.
 */
export const MERCHANT_RETURN_STATUS_IDS: readonly number[] = [20, 21, 22, 24, 23, RETURNED];

/** Le retour est accompli : le colis est rendu au vendeur. */
export const MERCHANT_RETURNED_STATUS_IDS: readonly number[] = [RETURNED_TO_CLIENT, RETURNED];

/** L'étape que le vendeur lit : « En route vers vous » ou « Rendu ». */
export function merchantReturnStage(statusId: number): 'on-the-way' | 'returned' {
  return MERCHANT_RETURNED_STATUS_IDS.includes(statusId) ? 'returned' : 'on-the-way';
}

/**
 * LE VOCABULAIRE DU TABLEAU DE BORD DE PILOTAGE — `docs/chantiers/dashboard.md`
 * § 3, D-161.
 *
 * Quatre ensembles nommés, écrits UNE fois : le serveur agrège avec, la recette
 * SQL recopie les mêmes listes, et l'écran construit ses liens de détail avec.
 *
 * - **Retours** : retourné (5), refusé (6) et tout le parcours RL- (20 → 24,
 *   23 clos). Le prompt du chantier le dit : « colis passés par un bon de retour
 *   ou statut retourné/refusé ». Le taux de réussite se calcule contre CET
 *   ensemble, et non contre le seul 5 comme l'ancien tableau de bord.
 * - **En transit** : physiquement en mouvement — expédié (7), acheminement
 *   (12), en cours de livraison (13), ramassage en cours (17).
 * - **Échecs** : familles `failure` ∪ `retry`, comme `ParcelsService.stats()`.
 * - **En cours** : tout ce qui n'est ni clos, ni retour, ni échec définitif —
 *   familles `initial` ∪ `transit` ∪ `pivot` ∪ `retry`. Contient « en transit ».
 */
export const DASHBOARD_RETURN_STATUS_IDS: readonly number[] = [5, 6, 20, 21, 22, 23, 24];
export const DASHBOARD_IN_TRANSIT_STATUS_IDS: readonly number[] = [7, 12, 13, 17];
export const DASHBOARD_FAILED_STATUS_IDS: readonly number[] = PARCEL_STATUSES.filter(
  (status) => status.group === 'failure' || status.group === 'retry',
).map((status) => status.id);
export const DASHBOARD_IN_PROGRESS_STATUS_IDS: readonly number[] = PARCEL_STATUSES.filter(
  (status) =>
    status.group === 'initial' ||
    status.group === 'transit' ||
    status.group === 'pivot' ||
    status.group === 'retry',
).map((status) => status.id);
export const DASHBOARD_CLOSED_STATUS_IDS: readonly number[] = PARCEL_STATUSES.filter(
  (status) => status.group === 'terminal',
).map((status) => status.id);

/**
 * Couleurs des statuts de circuit — DOMAIN.md §1.13 (`boxe_statuts`, 5 à 9).
 * Elles vivent ici, avec celles des colis : aucune page ne choisit sa palette.
 */
export const ROUTE_STATUS_COLORS: Record<RouteStatus, `#${string}`> = {
  draft: '#78716C',
  dispatched: '#3498DB',
  delivering: '#4B4B4B',
  collecting: '#8C7AE6',
  closed: '#6AB04C',
};

/**
 * Couleurs des états du bon de retour colis — chantier « Retours, BL Clients
 * Pro », point 1. Elles vivent ici, avec les autres : aucune page ne choisit sa
 * palette, et les trois espaces montrent le même état de la même couleur
 * (point 7).
 *
 * La progression va du gris de l'attente au vert de la clôture, en passant par
 * le bleu de la route ; l'annulation est rouge, et c'est le seul rouge.
 */
export const RETURN_HANDOVER_STATUS_COLORS: Record<ReturnHandoverStatus, `#${string}`> = {
  draft: '#78716C',
  'return-requested': '#D35400',
  'picked-up': '#2980B9',
  'awaiting-receipt': '#8C7AE6',
  confirmed: '#3498DB',
  closed: '#6AB04C',
  cancelled: '#D63031',
};

/**
 * Couleurs du BL et de son bon — chantier « Retours, BL Clients Pro », point 5.
 *
 * Les mêmes que le bon de retour colis, marche pour marche : c'est le même
 * voyage — le livreur a, l'entrepôt reçoit, l'affaire se classe — et un agent
 * qui passe d'un écran à l'autre ne doit pas réapprendre un code couleur.
 */
export const DELIVERY_SLIP_STATUS_COLORS: Record<DeliverySlipStatus, `#${string}`> = {
  collected: '#2980B9',
  'handed-over': '#8C7AE6',
  received: '#3498DB',
  returned: '#F97F51',
  closed: '#6AB04C',
};

export const BL_NOTE_STATUS_COLORS: Record<BlNoteStatus, `#${string}`> = {
  draft: '#78716C',
  'awaiting-receipt': '#8C7AE6',
  received: '#3498DB',
  returned: '#F97F51',
  closed: '#6AB04C',
};

/** Couleurs des statuts de bon — DOMAIN.md §1.12 (`bon_livraison_statuts`, 1 et 2). */
export const NOTE_STATUS_COLORS: Record<NoteStatus, `#${string}`> = {
  'awaiting-validation': '#FFA801',
  // La même teinte que le statut colis 18, et pour la même raison : c'est le
  // même fait, vu du bon plutôt que du carton (retouches Anass, point 5).
  'picked-up-by-courier': '#2980B9',
  validated: '#6AB04C',
};

/**
 * La couleur d'une FAMILLE de statuts — M5.
 *
 * Le suivi public ne montre pas les trente-huit statuts : il montre leurs
 * familles (D-060 — un e-commerçant n'a pas à lire « troisième appel sans
 * réponse »). La timeline a donc besoin d'une couleur par famille, et la
 * déduire « du premier statut du groupe » aurait fait dépendre la teinte de
 * l'ordre de déclaration du référentiel.
 *
 * Elles reprennent les statuts les plus représentatifs de chaque famille —
 * **sauf deux**, et c'est un test qui l'a imposé.
 *
 * `transit` valait `#2F3640` et `retry` `#341F97`, les couleurs de « expédié »
 * et de « premier appel ». Elles sont quasi noires : sur le fond sombre
 * (`#171717`) elles font **1,47:1** et **1,53:1** — une pastille invisible, donc
 * un fil de timeline sans ses points. Le mode sombre n'existait pas quand ces
 * couleurs ont été choisies pour des BADGES sur fond clair.
 *
 * Les deux sont remontées vers un ton moyen, qui se voit sur les deux fonds
 * (4,37:1 et 4,10:1 pour `transit`, 3,99:1 et 4,49:1 pour `retry`), et
 * `status-group-colors.spec.ts` refuse désormais toute famille invisible d'un
 * côté ou de l'autre.
 */
export const STATUS_GROUP_COLORS: Record<ParcelStatusGroup, `#${string}`> = {
  initial: '#D35400',
  transit: '#607D8B',
  pivot: '#4B4B4B',
  terminal: '#6AB04C',
  failure: '#D63031',
  retry: '#7C6CF0',
  return: '#F97F51',
};
