import {
  COURIER_RETURN_STATUS_IDS,
  findParcelStatus,
  PARCEL_STATUSES,
  RETURNED_TO_AGENCY,
} from './statuses';

/**
 * Machine à états du colis.
 *
 * DOMAIN.md §2.3 : « Aucune machine à états n'est déclarée » dans le legacy —
 * `ColisController::traking_form` a littéralement `$rules = []` puis affecte le
 * `statut_id` reçu sans le confronter au statut courant. C'est la dette que ce
 * fichier solde : la table ci-dessous est le SEUL endroit qui dit ce qu'un
 * statut autorise.
 *
 * Construction (DOMAIN.md §2.2) :
 *  - chemin nominal même ville  : 2 → 3 → 16 → 13 → 4
 *  - chemin nominal inter-ville : 2 → 3 → 7 → 13 → 4
 *  - chemin d'échec             : 13 → {6, 9, 27, 29, 32, 33} → 20 → 22 → 23
 *  - relances et reports        : 13 → {11, 14, 25, 26, 32..35} → {4, 13, 20}
 *
 * Ce qui est délibérément EXCLU alors qu'observé en prod :
 *  - `4 → 6` (39 occurrences) et `4 → 13` (19) : sortir d'un état terminal rend
 *    la facturation non déterministe (DOMAIN.md §2.2). Un colis livré ou
 *    retourné-récupéré ne bouge plus — il se conteste (`DISPUTE_TARGETS`).
 *  - `… → 5` (595 occurrences pour le seul `6 → 5`) : depuis C3, un colis
 *    n'entre dans le retour que par `20`, jamais directement par `5`. Le
 *    parcours de retour est `20 → 22 → 23` et rien d'autre — c'est ce qui rend
 *    une remise livreur et un bon de retour vérifiables. Cf. `DECISIONS.md`
 *    D-024.
 */

/** Statuts terminaux verrouillés : ils ne sortent que par une contestation. */
export const LOCKED_STATUS_IDS: readonly number[] = [4, 23];

/**
 * Transitions autorisées, par statut source.
 * Une clé absente = statut sans suite définie (l'UI n'y propose rien).
 */
const TRANSITIONS: Readonly<Record<number, readonly number[]>> = {
  // --- Initial ---------------------------------------------------------------
  // 1 Attente de ramassage : le livreur passe à l'adresse.
  //
  // `18` s'y ajoute — retouches Anass, point 5. Un bon peut être validé par le
  // livreur sans que le circuit soit parti : le colis va alors de `1`
  // directement à « ramassé par le livreur », sans passer par `17`.
  1: [3, 17, 18, 19, 27, 38],
  // 2 En attente d'arrivée : le client dépose en agence.
  2: [3, 4, 19, 27, 38],
  // 38 Programmé : rejoint le flux normal à la date prévue.
  38: [1, 2, 3, 19, 27],

  // --- Pivots ----------------------------------------------------------------
  // 3 Ramassé : dispatch local (16), expédition inter-ville (7), ou stock (31).
  3: [4, 7, 12, 16, 19, 31],
  /*
   * 16 En attente de livraison : mise en circuit — et, depuis le chantier
   * « Correctifs avant démo » (point 3), RETOUR SUR LE QUAI par `3`.
   *
   * La clé de voûte du point 3 : un circuit de livraison ne prend plus que
   * `3 Colis ramassé`. Sans ce cran, `16` devenait un cul-de-sac — le colis n'y
   * avait plus aucune sortie vers un circuit, et rien dans la table ne le
   * ramenait à `3`. Quatorze colis de la production s'y trouvaient déjà, sans
   * circuit pour les porter.
   *
   * Le cran est LOGIQUE et non un rattrapage : les deux états décrivent un
   * colis à l'entrepôt. `3` dit « sur le quai, prêt à charger », `16` dit « mis
   * de côté pour une tournée ». Revenir de l'un à l'autre est un geste de quai,
   * pas une invention : la marchandise n'a pas bougé de bâtiment.
   *
   * Il ferme aussi la boucle de `31 En stock`, qui rejoint `16` et n'avait pas
   * davantage de chemin vers `3`.
   */
  16: [3, 4, 7, 13, 31, 37],
  // 13 En cours de livraison : le carrefour réel du métier.
  13: [4, 6, 8, 9, 10, 11, 14, 25, 26, 27, 28, 29, 32, 33, 34, 35, 37],
  // 31 En stock : ressort en circuit.
  31: [13, 16, 20],

  // --- Transit ---------------------------------------------------------------
  // 7 Expédié : arrivée en ville de destination.
  7: [4, 13, 14, 16, 37],
  // 12 En cours d'expédition.
  12: [7, 16, 37],
  /*
   * 17 En cours de ramassage → 18, et non plus 3 — retouches Anass, point 5.
   *
   * « Le statut Ramassé n'est posé que par cette validation », celle de
   * l'agence à l'arrivée à l'entrepôt. Le livreur, lui, atteste seulement
   * d'avoir la marchandise en main : c'est `18`.
   *
   * La différence n'est pas cosmétique. `3 Colis ramassé` fait entrer le colis
   * dans le circuit de livraison et déclenche l'exonération de ramassage
   * (§3.4) ; le poser depuis un trottoir revenait à dire que la marchandise
   * était à l'entrepôt alors qu'elle était dans une sacoche. Un colis perdu
   * entre les deux n'était plus repérable — il était « ramassé ».
   */
  17: [8, 18, 19, 27],

  // 18 Ramassé par le livreur : l'agence réceptionne (3), ou la course tombe.
  18: [3, 19, 27],

  // --- Relances et reports ---------------------------------------------------
  // Toutes les relances retombent sur le même triangle : livrer, reporter, rendre.
  11: [4, 13, 14, 20, 32, 34],
  32: [4, 13, 14, 20, 33, 35],
  33: [4, 13, 14, 20],
  34: [4, 13, 14, 20, 32],
  35: [4, 13, 14, 20, 33],
  25: [4, 11, 13, 14, 20],
  26: [4, 13, 14, 20],
  14: [4, 13, 20],

  // --- Échecs ----------------------------------------------------------------
  6: [13, 20],
  8: [13, 20, 37],
  9: [4, 11, 13, 20],
  10: [13, 20],
  19: [20],
  27: [20],
  28: [20],
  29: [20],
  37: [13, 16, 20],

  // --- Retour (C3) -----------------------------------------------------------
  // Le parcours est 20 → 22 → 23, et rien d'autre :
  //   20 RETURN_PENDING     — le colis est à retourner
  //   22 RETURNED_TO_AGENCY — remis par le livreur, réceptionné en agence
  //   23 RETURNED_TO_CLIENT — rendu au client, terminal
  // 21 « En cours de récupération » et 24 « En cours de retour » restent
  // atteignables : 4 colis de production les portent, les en retirer les
  // piégerait. Ils ne sont produits par aucun geste de la v2.
  20: [21, 22, 24],
  21: [22, 24],
  22: [23, 24],
  24: [22, 23],
  // 5 « Retourné » est l'ancien pivot du legacy (755 colis, 2 303 événements).
  // Plus aucune transition n'y MÈNE en v2 — on entre dans le retour par 20 —
  // mais on en SORT vers le parcours v2, pour que les colis repris rejoignent
  // le flux au lieu d'y rester bloqués.
  5: [22, 23],

  // --- Terminaux -------------------------------------------------------------
  // 4 Livré et 23 Retour récupéré : aucune suite. Voir LOCKED_STATUS_IDS.
  4: [],
  23: [],
};

/**
 * Cibles d'une contestation : la seule porte de sortie d'un statut terminal.
 * Réservée au staff, commentaire obligatoire, et tracée comme tout événement.
 * DOMAIN.md §2.2 — c'est la version encadrée des 39 transitions `4 → 6`
 * observées en prod.
 */
export const DISPUTE_TARGETS: readonly number[] = [6, 9, 13, 28, 37];

/**
 * LES PASSAGES DE LA RÉCEPTION D'UN RL- — lot W3, E2E-011 (D-238).
 *
 * L'agence qui réceptionne le bon de retour d'un livreur pose « Récupéré par
 * livreur » (22) sur chaque colis rapporté, depuis l'état où il était : ce
 * sont les états qu'un RL- accepte (`COURIER_RETURN_STATUS_IDS`). Ce passage
 * est un geste SYSTÈME, pas une transition qu'un écran propose : il n'entre
 * pas dans `allowedTransitions()`, qui reste le garde à l'exécution. Il est
 * écrit au référentiel `parcel_transitions` sous son propre type
 * (`RETURN_RECEPTION`), pour que la table dise ce que la base contient
 * (invariant ST-05). Un passage vers 22 depuis un autre état reste signalé.
 */
export const RETURN_RECEPTION_TRANSITIONS: ReadonlyArray<readonly [number, number]> =
  COURIER_RETURN_STATUS_IDS.map((from) => [from, RETURNED_TO_AGENCY] as const);

export function isLockedStatus(statusId: number): boolean {
  return LOCKED_STATUS_IDS.includes(statusId);
}

/**
 * LA DESTINATION EST VERROUILLÉE — chantier « Étiquette orange », point 3.
 *
 * « Le bouton “Changer la destination” disparaît de la fiche des colis Livré,
 * Retourné, Annulé et clôturés ; l'API renvoie 422 avec message clair sur ces
 * statuts. »
 *
 * **Ce n'est pas la même liste que `LOCKED_STATUS_IDS`**, et c'est pour cela
 * qu'elle a son nom. Le verrou terminal répond à « ce colis peut-il encore
 * bouger ? » : `4` et `23` seuls, parce qu'eux seuls se contestent. Celle-ci
 * répond à « peut-on encore décider où il va ? », et la réponse est non bien
 * plus tôt :
 *
 * | id | statut | pourquoi l'adresse est figée |
 * |---|---|---|
 * | `4` | Livré | il est arrivé ; réécrire l'adresse ferait mentir la preuve |
 * | `5` | Retourné | il est reparti chez le vendeur |
 * | `23` | Retour récupéré | le vendeur l'a repris, l'affaire est close |
 * | `19` | Annulé par le vendeur | il n'y a plus de livraison à rediriger |
 * | `27` | Annulé par le client | idem |
 *
 * C'était déjà refusé sur `4` et `23` (par `isLockedStatus`) et OUVERT sur les
 * trois autres : on pouvait donner une nouvelle adresse à un colis annulé la
 * semaine précédente, et le tarif se recalculait avec.
 */
export const DESTINATION_LOCKED_STATUS_IDS: readonly number[] = [4, 5, 23, 19, 27];

/** Peut-on encore décider où ce colis va ? */
export function isDestinationLocked(statusId: number): boolean {
  return DESTINATION_LOCKED_STATUS_IDS.includes(statusId);
}

/** Statuts atteignables depuis `statusId`, hors contestation. */
export function allowedTransitions(statusId: number): number[] {
  return [...(TRANSITIONS[statusId] ?? [])].filter((id) => findParcelStatus(id) !== undefined);
}

export function canTransition(from: number, to: number): boolean {
  return allowedTransitions(from).includes(to);
}

/** Statuts atteignables par contestation depuis un statut verrouillé. */
export function disputeTransitions(statusId: number): number[] {
  if (!isLockedStatus(statusId)) return [];
  return DISPUTE_TARGETS.filter((id) => id !== statusId && findParcelStatus(id) !== undefined);
}

/**
 * Contrôle d'intégrité du référentiel : toute cible doit exister dans
 * `PARCEL_STATUSES`. Exporté pour être appelable depuis un test ou un seed.
 */
export function unknownTransitionTargets(): number[] {
  const targets = new Set<number>();
  for (const list of Object.values(TRANSITIONS)) {
    for (const id of list) {
      if (!findParcelStatus(id)) targets.add(id);
    }
  }
  return [...targets];
}

/* --- Ce qu'un LIVREUR rapporte du terrain — M1, D-074 -------------------- */

/**
 * Les statuts depuis lesquels un livreur a une COURSE en cours.
 *
 * `13 En cours de livraison` est le cas normal : la tournée est partie. `7
 * Expédié` couvre l'inter-ville qu'il transporte sans être passé par `16`.
 *
 * **`17 En cours de ramassage` s'y ajoute — C12 bloc 3.** Un circuit de
 * ramassage parti pose `17` sur ses colis, et le livreur doit pouvoir
 * rapporter ce qu'il a trouvé à l'adresse : ramassé (`3`), hors zone (`8`), ou
 * annulé par le client (`27`). Sans ce statut ici, `courierOutcomesFor()`
 * rendait un tableau vide et l'application n'affichait aucun bouton sur une
 * tournée de ramassage — le livreur ramassait, et personne ne l'apprenait.
 *
 * `24 En cours de retour` n'y est PAS, et c'est délibéré : un retour se clôt
 * par le bon de retour signé du client, pas par un appui sur un téléphone.
 * C'est la remise vérifiable de C3 (D-024), et l'ouvrir ici la contournerait.
 * Depuis le lot C (D-179), ses deux issues s'ouvrent au livreur par une table
 * à part, `COURIER_RETURN_ROUTE_OUTCOMES`, et seulement celles-là.
 *
 * Depuis tout autre statut, le colis n'est pas chez lui — il est en agence, en
 * stock, ou déjà terminé — et son application n'a rien à en dire.
 */
/**
 * Les familles qu'un livreur a POSÉES lui-même, et qu'il peut donc revoir —
 * retouches Anass, point 6.
 *
 * « Les statuts intermédiaires (Pas de réponse, Reporté, etc.) restent
 * modifiables par le livreur, y compris vers Livré. »
 *
 * C'est l'écart que le point 6 a mis au jour : `courierOutcomesFor(11)` rendait
 * un tableau VIDE, parce que `11 Premier appel sans réponse` n'était pas une
 * SOURCE. Un livreur qui notait « pas de réponse » à 11 h et à qui le client
 * rappelait à 11 h 05 n'avait plus aucun bouton — il livrait, et le colis
 * restait « sans réponse » jusqu'à ce que l'agence le corrige.
 *
 * Les deux familles suffisent, et elles se DÉCRIVENT plutôt que de s'énumérer :
 * `retry` (les relances, les reports) et `failure` (les échecs de course). Une
 * liste recopiée ici aurait cessé de suivre le référentiel au premier statut
 * ajouté.
 *
 * Rien ne s'ouvre pour autant : `courierOutcomesFor()` reste le produit de la
 * machine à états ET du filtre de bureau. Depuis `27 Annulé par le client`, la
 * seule suite est `20 Retour demandé`, qui est une décision d'agence — le
 * livreur n'y gagne aucun bouton. Depuis `11`, il retrouve « livré »,
 * « reporté » et l'appel suivant, qui est exactement ce que le point 6
 * demande.
 */
const COURIER_REVISABLE_GROUPS: readonly string[] = ['retry', 'failure'];

/**
 * Les statuts depuis lesquels un livreur a quelque chose à dire.
 *
 * Les trois premiers sont les courses EN COURS ; les suivants, les issues qu'il
 * a rapportées et qui peuvent encore changer le jour même.
 *
 * Un statut VERROUILLÉ n'y entre jamais (`LOCKED_STATUS_IDS`) : « Livré » posé
 * par le livreur ne se reprend pas depuis un trottoir — c'est le point 6, et
 * c'est ce qui rend la livraison attestable.
 */
export const COURIER_SOURCE_STATUS_IDS: readonly number[] = [
  13,
  7,
  17,
  ...PARCEL_STATUSES.filter(
    (status) => COURIER_REVISABLE_GROUPS.includes(status.group) && !isLockedStatus(status.id),
  ).map((status) => status.id),
];

/**
 * Les mouvements de BUREAU : un livreur ne les rapporte jamais.
 *
 * Ce ne sont pas des issues de course mais des décisions d'organisation —
 * mettre en stock, réexpédier, remettre en attente de livraison, ouvrir un
 * retour. Elles se prennent à l'agence, devant l'écran, avec le contexte que
 * le livreur n'a pas : les autres colis du client, la tournée du lendemain.
 *
 * Les laisser au livreur ferait deux dégâts. Un colis remis en `16` depuis la
 * rue disparaîtrait de sa tournée sans que personne l'ait décidé ; et le retour
 * (`20`) ouvert d'un téléphone court-circuiterait la remise physique que C3 a
 * précisément rendue vérifiable (D-024).
 */
const OFFICE_ONLY_STATUS_IDS: ReadonlySet<number> = new Set([
  /*
   * `3 Colis ramassé` rejoint la liste — retouches Anass, point 5. C'est la
   * RÉCEPTION à l'entrepôt, et elle se constate là-bas : le livreur pose `18`,
   * l'agence pose `3` en validant le bon. Les deux gestes étaient confondus.
   */
  3, 5, 7, 12, 13, 16, 17, 19, 20, 21, 22, 23, 24, 30, 31, 36, 38,
]);

/**
 * LE CIRCUIT DE RETOUR SUR LE TÉLÉPHONE — lot C, point C2 (D-179), qui amende
 * D-024 sur ce point précis.
 *
 * Un circuit de retour dispatché (CIR-2610-0039, Hamouda, 4 colis « En cours de
 * retour ») n'existait nulle part sur le mobile : l'agence rendait compte à sa
 * place depuis la page circuit, `24 → 23`. Le livreur rend désormais compte
 * lui-même, avec les DEUX issues que la machine à états ouvre depuis `24` —
 * celles que l'admin pose déjà :
 *
 *  - `23 Retour récupéré` : le vendeur a repris son colis, en tête (c'est
 *    l'issue attendue, `routeExpectedOutcome('return')`) ;
 *  - `22 Récupéré par le livreur` : non remis, le colis reste en main et
 *    redevient éligible à un circuit de retour (`ROUTE_ELIGIBLE_STATUS_IDS`).
 *
 * C'est une table à part, et non `24` ajouté aux sources : les deux issues sont
 * des mouvements « de bureau » partout ailleurs (`OFFICE_ONLY_STATUS_IDS`), et
 * elles ne s'ouvrent au livreur que sur le colis qu'il ROULE vers le vendeur.
 */
const COURIER_RETURN_ROUTE_OUTCOMES: Readonly<Record<number, readonly number[]>> = {
  24: [23, 22],
};

/**
 * Les issues qu'un LIVREUR peut rapporter depuis le statut courant.
 *
 * C'est `allowedTransitions()` moins les mouvements de bureau : la machine à
 * états reste la seule source de ce qui est possible, et cette fonction dit
 * seulement qui a le droit de le décider. Les dupliquer aurait produit deux
 * tables qui divergent au premier ajustement.
 *
 * Tableau VIDE quand le colis n'est pas en main : c'est ce que l'application
 * lit pour n'afficher aucun bouton, plutôt que d'en proposer qui échoueront.
 */
export function courierOutcomesFor(statusId: number, routeKind?: string): number[] {
  const onReturnRoute = COURIER_RETURN_ROUTE_OUTCOMES[statusId];
  if (onReturnRoute) return onReturnRoute.filter((id) => canTransition(statusId, id));

  if (routeKind !== undefined && routeKindKey(routeKind) === 'delivery') {
    const revision = courierDeliveryRevisionsFor(statusId);
    if (revision) return revision;
  }

  if (!COURIER_SOURCE_STATUS_IDS.includes(statusId)) return [];

  return allowedTransitions(statusId).filter((id) => !OFFICE_ONLY_STATUS_IDS.has(id));
}

/** Le livreur a-t-il le droit de rapporter CETTE issue depuis CE statut ? */
export function canCourierReport(from: number, to: number, routeKind?: string): boolean {
  return courierOutcomesFor(from, routeKind).includes(to);
}

/* --- Le livreur REVOIT son statut — chantier « Menu Livraison : liste unique » --- */

const OUT_FOR_DELIVERY_ID = 13;
const DELIVERED_ID = 4;

/**
 * LES ISSUES DE TERRAIN d'une livraison : tout ce qu'un livreur peut rapporter
 * d'un colis « En cours de livraison » (13). Dix-sept aujourd'hui, « Livré »
 * compris. Dérivée de la machine à états, jamais recopiée.
 */
export function courierDeliveryOutcomeIds(): number[] {
  return allowedTransitions(OUT_FOR_DELIVERY_ID).filter((id) => !OFFICE_ONLY_STATUS_IDS.has(id));
}

/**
 * CE COLIS EST-IL ENCORE « À LIVRER » POUR SON LIVREUR — règles C et D.
 *
 * Oui tant qu'il roule (13) ou qu'il porte une issue de terrain autre que
 * « Livré ». Non dès qu'il est livré, ou que l'agence l'a repris (16, 20, 22…).
 * C'est la définition de la liste unique du menu Livraison.
 */
export function isCourierDeliveryPending(statusId: number): boolean {
  if (statusId === DELIVERED_ID) return false;
  return statusId === OUT_FOR_DELIVERY_ID || courierDeliveryOutcomeIds().includes(statusId);
}

/**
 * LA RÉVISION — demande de Badr (SpeedyTrans), règle C : « tant qu'un colis
 * n'est pas livré, le livreur change son statut autant de fois qu'il veut ».
 *
 * La machine à états ne le permettait pas : depuis « Reporté » seule « Livré »
 * restait, depuis « Refusé » rien. Elle est écrite pour le BUREAU, où un refusé
 * repart par 13 ou par 20 — deux décisions d'agence.
 *
 * Cette table vit donc À CÔTÉ de la machine, et ne la modifie pas : le bureau
 * garde ses transitions au statut près. Elle ne s'ouvre qu'au LIVREUR, sur un
 * circuit de LIVRAISON (un « hors zone » de ramassage ne devient pas « livré »),
 * et seulement entre issues de terrain — 13, 16 et 20 restent à l'agence.
 * « Livré » (4) n'est source de rien : il est définitif (règle D).
 *
 * `null` = ce statut ne relève pas de la révision ; l'appelant retombe sur la
 * règle ordinaire.
 */
function courierDeliveryRevisionsFor(statusId: number): number[] | null {
  if (!isCourierDeliveryPending(statusId)) return null;
  return courierDeliveryOutcomeIds().filter((id) => id !== statusId);
}

/* --- L'ADMIN REVOIT un statut de terrain — D-245 ------------------------- */

/**
 * LES STATUTS DE TERRAIN que l'administrateur pose librement — D-245 (Badr,
 * 12/11/2026). Ce sont, à l'identité près, les issues qu'un livreur rapporte
 * d'une livraison, « Livré » EXCEPTÉ : injoignable, reporté, refusé, les
 * appels, mauvais numéro… Dérivée de `courierDeliveryOutcomeIds()`, donc de la
 * machine à états : la liste de l'admin ne peut pas diverger de celle du
 * livreur.
 *
 * Ce qui n'y est PAS, et ne se pose donc jamais par cette porte : tout statut
 * qui naît d'un bon, d'un circuit ou d'un encaissement (ramassé, expédié, en
 * cours de livraison, livré, les retours), et ceux d'avant le ramassage.
 */
export function adminFieldStatusIds(): number[] {
  return courierDeliveryOutcomeIds().filter((id) => id !== DELIVERED_ID);
}

/**
 * Les cibles libres depuis `statusId` : tous les autres statuts de terrain.
 * Tableau VIDE quand la source n'en est pas un — l'écran n'offre alors rien, et
 * le serveur refuse de toute façon.
 *
 * Cette table vit À CÔTÉ de la machine à états, comme la révision du livreur
 * (D-242) : le bureau garde ses transitions au statut près.
 */
export function adminFieldTargets(statusId: number): number[] {
  const field = adminFieldStatusIds();
  if (!field.includes(statusId)) return [];
  return field.filter((id) => id !== statusId);
}

/** L'admin a-t-il le droit de passer de CE statut de terrain à CELUI-LÀ ? */
export function canAdminReviseField(from: number, to: number): boolean {
  return adminFieldTargets(from).includes(to);
}

/* --- Ce qu'un CIRCUIT fait à ses colis — C12 bloc 3 ---------------------- */

/**
 * **L'éligibilité dépend du TYPE de circuit.** C'était le bug.
 *
 * Jusqu'ici, `routeCandidates()` proposait les mêmes colis quel que soit le
 * type : ceux en `16 En attente de livraison`, `3 Ramassé` ou `7 Expédié`,
 * dans la ville de DESTINATION. Un circuit de ramassage se remplissait donc de
 * colis déjà ramassés, et aucun colis en attente de ramassage n'y entrait —
 * autrement dit le type ne servait qu'à étiqueter une tournée de livraison.
 *
 * Deux axes, et les deux dépendent du type :
 *
 * | Type        | Statuts éligibles | La ville du circuit est celle… |
 * |-------------|-------------------|--------------------------------|
 * | `delivery`  | **3**             | de DESTINATION                 |
 * | `pickup`    | 1, 17             | d'ORIGINE                      |
 * | `return`    | 20, 22            | d'ORIGINE                      |
 *
 * **LA LIVRAISON NE PREND QUE `3 Colis ramassé` — chantier « Correctifs avant
 * démo », point 3.** « Ne proposer que les colis au statut Ramassé (à
 * l'entrepôt), non affectés à un circuit actif. Exclure En attente de
 * livraison, En cours de livraison et tout colis déjà dans un circuit non
 * clôturé. »
 *
 * La liste en portait quatre, et c'est ce qui remplissait le panneau
 * « Ajouter des colis » de cartons qui ne sont pas sur le quai :
 *
 *  - `16 En attente de livraison` est nommément exclu par la mission. C'est
 *    l'état d'un colis qu'un circuit a DÉJÀ emporté puis rendu à l'attente ;
 *  - `7 Expédié` désigne un inter-ville EN ROUTE, pas arrivé. Il entrera dans
 *    un circuit quand l'agence de destination l'aura reçu, c'est-à-dire passé
 *    en `3` ;
 *  - `31 En stock` était entré aux retouches Anass point 8, pour que le
 *    scanner au dépôt ne refuse pas un colis entreposé. La mission le reprend :
 *    l'entreposage est un service vendu, pas une étape de tournée, et un colis
 *    en stock sort par un bon de dépôt qui le repose en `3`.
 *
 * Le colis n'est donc éligible qu'une fois **physiquement sur le quai**, ce que
 * `3` est le seul statut à dire. La machine à états n'a pas bougé : le départ
 * continue de passer par le cran `16` (`ROUTE_DISPATCH_HOPS`), avec sa ligne
 * d'historique.
 *
 * **`17` est éligible au ramassage en plus de `1`** : une tournée de ramassage
 * non clôturée la veille laisse des colis « en cours de ramassage » qu'il faut
 * pouvoir reprendre le lendemain, sinon ils ne rentrent dans aucun circuit.
 *
 * **La ville d'ORIGINE pour le ramassage et le retour**, parce que le livreur
 * va CHEZ L'EXPÉDITEUR dans les deux cas : chercher la marchandise, ou la lui
 * rendre. Filtrer sur la destination y était doublement faux — mauvais colis,
 * et mauvaise ville.
 *
 * Les trois listes restent DISJOINTES, et c'est ce qui rend
 * `routeKindForParcelStatus()` sûr : le scan mobile déduit le type du circuit
 * sans rien demander au livreur.
 */
const ROUTE_ELIGIBLE_STATUS_IDS: Readonly<Record<string, readonly number[]>> = {
  delivery: [3],
  pickup: [1, 17],
  return: [20, 22],
};

/**
 * Le statut que le DÉPART d'un circuit pose sur ses colis.
 *
 *  - `delivery` → `13 En cours de livraison` ;
 *  - `pickup`   → `17 En cours de ramassage` ;
 *  - `return`   → `24 En cours de retour`.
 *
 * **`24` était réputé mort** : le commentaire de `TRANSITIONS` dit que `21` et
 * `24` « ne sont produits par aucun geste de la v2 ». Ils le sont désormais —
 * `24` l'est. C'est le seul statut atteignable depuis `20` COMME depuis `22`,
 * et son libellé dit exactement ce qui se passe : le livreur roule avec la
 * marchandise qu'il rapporte au client. En inventer un autre aurait demandé
 * d'ajouter une transition pour un état que le référentiel nommait déjà.
 */
const ROUTE_DISPATCH_STATUS_ID: Readonly<Record<string, number>> = {
  delivery: 13,
  pickup: 17,
  return: 24,
};

/**
 * Les crans INTERMÉDIAIRES tolérés au départ, par type.
 *
 * Un colis en `3 Ramassé` n'a pas le droit d'aller directement en `13` : il
 * passe par `16`, avec son propre événement. Deux crans, deux lignes
 * d'historique — plutôt qu'une transition inventée pour l'occasion.
 *
 * Les deux autres types n'en ont pas besoin : `1 → 17`, `20 → 24` et `22 → 24`
 * sont des transitions directes de la machine à états.
 */
const ROUTE_DISPATCH_HOPS: Readonly<Record<string, readonly number[]>> = {
  delivery: [16],
};

/** Le type, normalisé : `PICKUP` en base, `pickup` sur le fil. */
function routeKindKey(kind: string): string {
  return kind.toLowerCase();
}

/** Statuts qu'un colis doit porter pour entrer dans un circuit de ce type. */
export function routeEligibleStatusIds(kind: string): number[] {
  return [...(ROUTE_ELIGIBLE_STATUS_IDS[routeKindKey(kind)] ?? [])];
}

/** Ce colis peut-il entrer dans un circuit de ce type ? */
export function isRouteEligibleStatus(kind: string, statusId: number): boolean {
  return routeEligibleStatusIds(kind).includes(statusId);
}

/** Le statut posé par le départ d'un circuit de ce type. */
export function routeDispatchStatusId(kind: string): number {
  return ROUTE_DISPATCH_STATUS_ID[routeKindKey(kind)] ?? ROUTE_DISPATCH_STATUS_ID.delivery!;
}

/**
 * Le chemin d'un colis vers la tournée, cran par cran. `null` = impossible.
 *
 * Tableau VIDE quand le colis y est déjà : un circuit de ramassage qui reprend
 * un colis resté en `17` ne doit pas écrire un événement qui ne dit rien.
 *
 * Chaque cran est validé par `canTransition()`, jamais par une liste recopiée
 * ici : la machine à états reste la seule source de ce qui est possible.
 */
export function routeDispatchPath(kind: string, from: number): number[] | null {
  const target = routeDispatchStatusId(kind);

  if (from === target) return [];
  if (canTransition(from, target)) return [target];

  for (const hop of ROUTE_DISPATCH_HOPS[routeKindKey(kind)] ?? []) {
    if (canTransition(from, hop) && canTransition(hop, target)) return [hop, target];
  }

  return null;
}

/**
 * L'issue que le livreur rapporte le plus souvent, par type — celle que
 * l'écran met en avant.
 *
 * `18 Ramassé par le livreur` pour un ramassage, `4 Livré` pour une livraison,
 * `23 Retour récupéré` pour un retour. Ce n'est PAS une restriction : les autres issues
 * restent proposées par `courierOutcomesFor()`, qui les tient de la machine à
 * états. C'est seulement l'ordre dans lequel on les montre à quelqu'un debout
 * sur un trottoir.
 */
export function routeExpectedOutcome(kind: string): number {
  const key = routeKindKey(kind);
  // `18` et non `3` depuis les retouches Anass, point 5 : le livreur atteste
  // avoir la marchandise, l'agence atteste l'avoir reçue.
  if (key === 'pickup') return 18;
  if (key === 'return') return 23;
  return 4;
}

/**
 * CE COLIS ATTEND-IL ENCORE L'ACTION EN MASSE DE SON CIRCUIT — recette 6,
 * point 2.
 *
 * CIR-2610-0012 : après « Marquer ramassés par le livreur (7) », les sept
 * colis restaient cochables et le bouton reproposait « (7) ». « Tout cocher »
 * et l'action ne portent plus que sur ce qui reste à faire :
 *
 *  - **ramassage** : le colis n'est pas encore « Ramassé par le livreur » (18)
 *    ni « Ramassé » (3) — le statut fait foi, pas l'ancienne case « en main » ;
 *  - **livraison, retour** : aucun résultat rapporté (`confirmed`).
 *
 * Web (page circuit) et mobile (écran circuit) lisent la même règle.
 */
export function routeParcelAwaitsOutcome(
  kind: string,
  parcel: { statusId: number; confirmed: boolean },
): boolean {
  if (routeKindKey(kind) === 'pickup') return parcel.statusId !== 18 && parcel.statusId !== 3;
  return !parcel.confirmed;
}

/**
 * Ce circuit va-t-il CHEZ L'EXPÉDITEUR — C12 bloc 4.
 *
 * Vrai pour le ramassage et pour le retour : dans les deux cas le livreur se
 * rend à l'adresse de l'e-commerçant, pour prendre la marchandise ou pour la
 * lui rendre. L'écran, la feuille de route et les boutons « appeler » et
 * « itinéraire » s'y règlent — afficher le destinataire sur une tournée de
 * ramassage envoyait le livreur à l'autre bout de la ville.
 *
 * Faux pour la livraison, qui va chez le destinataire. Inchangé.
 */
export function routeTargetsSender(kind: string): boolean {
  const key = routeKindKey(kind);
  return key === 'pickup' || key === 'return';
}

/**
 * Le TYPE de circuit qu'un colis appelle, d'après son statut — C12 bloc 3.
 *
 * C'est ce qui permet au scan mobile de ne rien demander au livreur : il vise
 * un code-barres, et le statut du colis dit déjà ce qu'on vient faire.
 * `1 En attente de ramassage` ne peut être scanné que pour un ramassage,
 * `16 En attente de livraison` que pour une livraison.
 *
 * `null` quand aucun type ne l'accepte : le colis n'est prêt pour rien, et le
 * scan le refuse avec le motif `parcel-bad-status` — c'est plus utile que
 * d'ouvrir une tournée au hasard.
 *
 * Les listes d'éligibilité ne se recouvrent pas, ce qui rend la déduction sûre.
 * Le jour où elles se recouvriraient, ce serait une ambiguïté réelle, et
 * l'appelant devrait dire quel type il veut — c'est pour cela que la route de
 * scan accepte un type EXPLICITE, qui prime toujours sur cette déduction.
 */
export function routeKindForParcelStatus(statusId: number): string | null {
  for (const kind of ['delivery', 'pickup', 'return']) {
    if (isRouteEligibleStatus(kind, statusId)) return kind;
  }

  return null;
}
