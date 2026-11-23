import { PARCEL_STATUSES, type ParcelStatusDefinition, findParcelStatus } from './statuses';

/**
 * LES NEUF STATUTS QUE LE LIVREUR LIT — L2 bloc 7, maquette 14.
 *
 * Le référentiel en compte **trente-quatre** (DOMAIN.md §2.1). Anass en montre
 * **neuf**, et il a raison : « troisième appel sans réponse » et « deuxième
 * appel + SMS » sont des états de PLATEAU, pas de terrain. Un livreur qui
 * cherche à comprendre où en est son colis n'a pas à lire le vocabulaire du
 * service client.
 *
 * **C'est un regroupement, pas une seconde liste.** Chaque famille nomme les
 * statuts du référentiel qu'elle couvre, et `courier-statuses.spec.ts` vérifie
 * que la **partition est exacte** : tout statut est dans une famille et une
 * seule. Sans ce test, un statut ajouté au référentiel disparaîtrait
 * silencieusement de l'écran du livreur — c'est-à-dire qu'un de ses colis
 * prendrait un état qu'il ne peut pas lire.
 *
 * **Deux familles ne correspondent à rien d'unique**, et c'est la décision que
 * l'audit avait laissée ouverte :
 *
 *  - **« Nouveau »** — le référentiel n'a pas d'état « créé ». Un colis naît
 *    « en attente de ramassage » (1) si on vient le chercher, ou « en attente
 *    d'arrivée » (2) si le vendeur le dépose. « Nouveau » couvre donc ce
 *    dernier cas et la programmation (38), et « À ramasser » garde le premier :
 *    c'est ce qui correspond au geste du livreur, qui va chercher l'un et pas
 *    l'autre.
 *  - **« Retour demandé »** — il couvre l'attente de retour (20) et la
 *    récupération en cours (21), c'est-à-dire le moment où la décision est
 *    prise mais où le colis n'est pas encore reparti.
 *
 * **« Échec de livraison » couvre aussi les RELANCES**, et c'est délibéré.
 * Anass l'écrit « Client absent / Adresse incorrecte » : « client absent » est
 * exactement la famille `retry` du référentiel. Pour le service client, un
 * premier appel sans réponse n'est pas un échec — pour le livreur qui est
 * devant la porte, si.
 *
 * **La couleur vient du référentiel**, par un statut REPRÉSENTATIF nommé ici.
 * La déduire du premier statut de la famille ferait dépendre la teinte de
 * l'ordre de déclaration (la leçon de `STATUS_GROUP_COLORS`), et en inventer
 * une neuve ferait un troisième vocabulaire de couleurs.
 *
 * **Deux familles peuvent partager une teinte, et c'est fidèle** : la maquette
 * peint elle-même « À ramasser », « Retour demandé » et « En retour » en
 * orange. Ce qui ne doit JAMAIS se confondre, ce sont les cinq issues — livré,
 * échec, retour demandé, en retour, retour effectué — et le test le garde :
 * lire « retourné » pour « livré » coûte un colis et une conversation.
 */
/**
 * Les neuf clés, en union FERMÉE — L2 bloc 9.
 *
 * `string` compilerait, et `t(family.key)` ne serait plus vérifié côté web :
 * une famille ajoutée sans sa traduction s'afficherait alors sous son
 * identifiant, en production, sans que rien ne l'ait signalé.
 */
export type CourierStatusKey =
  | 'new'
  | 'to-pickup'
  | 'picked-up'
  | 'out-for-delivery'
  | 'delivered'
  | 'failed'
  | 'return-requested'
  | 'returning'
  | 'returned';

export interface CourierStatusFamily {
  /** Clé i18n, résolue sous `courierStatus.<key>` par l'application. */
  key: CourierStatusKey;
  /** Le statut dont la famille emprunte la couleur. */
  representative: number;
  /** Les identifiants du référentiel que la famille couvre. */
  statusIds: readonly number[];
}

export const COURIER_STATUS_FAMILIES: readonly CourierStatusFamily[] = [
  /* 2 en attente d'arrivée · 38 programmé */
  { key: 'new', representative: 38, statusIds: [2, 38] },
  /* 1 en attente de ramassage · 17 ramassage en cours */
  { key: 'to-pickup', representative: 1, statusIds: [1, 17] },
  /*
   * 3 ramassé · 7 expédié · 12 expédition en cours · 16 en attente de
   * livraison · 18 ramassé par le livreur · 31 en stock
   *
   * `18` y entre avec les retouches Anass (point 5) : du point de vue du
   * LIVREUR, un colis qu'il a dans sa sacoche est ramassé. La distinction
   * entre « je l'ai » et « l'entrepôt l'a reçu » compte pour l'agence et pour
   * la facturation ; sur l'écran du terrain, ce sont deux mots pour un même
   * carton, et la famille en couvre déjà cinq.
   */
  { key: 'picked-up', representative: 3, statusIds: [3, 7, 12, 16, 18, 31] },
  /* 13 en cours de livraison */
  { key: 'out-for-delivery', representative: 13, statusIds: [13] },
  /* 4 livré */
  { key: 'delivered', representative: 4, statusIds: [4] },
  /*
   * Les échecs ET les relances : voir l'en-tête. Dix-sept statuts pour une
   * ligne d'écran, et c'est tout l'intérêt du regroupement.
   */
  {
    key: 'failed',
    representative: 6,
    statusIds: [6, 8, 9, 10, 11, 14, 19, 25, 26, 27, 28, 29, 32, 33, 34, 35, 37],
  },
  /* 20 en attente de retour · 21 récupération en cours */
  { key: 'return-requested', representative: 21, statusIds: [20, 21] },
  /* 22 récupéré par le livreur · 24 retour en cours */
  { key: 'returning', representative: 22, statusIds: [22, 24] },
  /* 5 retourné · 23 retour remis */
  { key: 'returned', representative: 5, statusIds: [5, 23] },
];

/** La couleur d'une famille : celle de son statut représentatif. */
export function courierStatusColor(family: CourierStatusFamily): string {
  return findParcelStatus(family.representative)?.color ?? '#4B4B4B';
}

/**
 * La famille d'un statut donné — `undefined` si le référentiel a bougé sans
 * que ce fichier suive, ce que le test empêche d'arriver en silence.
 */
export function courierStatusFamilyOf(statusId: number): CourierStatusFamily | undefined {
  return COURIER_STATUS_FAMILIES.find((family) => family.statusIds.includes(statusId));
}

/** Les définitions du référentiel couvertes par une famille, pour l'écran. */
export function courierStatusMembers(family: CourierStatusFamily): ParcelStatusDefinition[] {
  return PARCEL_STATUSES.filter((status) => family.statusIds.includes(status.id));
}
