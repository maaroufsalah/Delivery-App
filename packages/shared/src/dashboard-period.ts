/**
 * LA PÉRIODE D'UN TABLEAU DE BORD — chantier « Filtre de période ».
 *
 * Cinq choix, un seul vocabulaire, partagé par les deux tableaux de bord —
 * principal et financier — et par les deux côtés du fil. Le RÉSOLVEUR, lui,
 * vit côté API (`apps/api/src/common/dashboard/period.ts`) : il a besoin du
 * fuseau du tenant et de la première donnée en base, que le navigateur n'a pas.
 * Ce fichier ne porte que le contrat et ce qui est PUR.
 */

/**
 * Les périodes. `custom` est le seul qui lit `from` / `to` ; les autres les
 * IGNORENT — la borne se déduit du choix et du fuseau du tenant, elle ne se
 * saisit pas.
 *
 * Cinq périodes ont rejoint les cinq d'origine au chantier « Dashboard de
 * pilotage » (D-157) : `yesterday`, `last7`, `last30` (jours glissants, jour
 * courant compris), `prevMonth` (mois civil précédent, entier) et `year`
 * (depuis le 1er janvier). Aucune n'a disparu : les cookies et les liens
 * partagés qui portent `week` ou `month` restent valides.
 *
 * L'ORDRE est celui du sélecteur : du plus court au plus long, puis « depuis
 * le début » et « personnalisé ».
 */
export const DASHBOARD_PERIODS = [
  'today',
  'yesterday',
  'week',
  'last7',
  'last30',
  'month',
  'prevMonth',
  'year',
  'all',
  'custom',
] as const;

export type DashboardPeriod = (typeof DASHBOARD_PERIODS)[number];

/**
 * « Ce mois » par défaut : c'est la question qu'un exploitant se pose neuf fois
 * sur dix, et c'est la valeur que l'ancien tableau de bord approchait déjà avec
 * sa fenêtre figée de quatorze jours.
 */
export const DEFAULT_DASHBOARD_PERIOD: DashboardPeriod = 'month';

export function isDashboardPeriod(value: string | undefined | null): value is DashboardPeriod {
  return value != null && (DASHBOARD_PERIODS as readonly string[]).includes(value);
}

/**
 * Le pas d'agrégation du graphique « Colis par jour ».
 *
 * Une année en points journaliers, c'est trois cent soixante-cinq barres
 * illisibles ; le graphique change de PAS selon la longueur pour tenir la
 * lisibilité, et c'est le serveur qui agrège — l'écran ne recompte pas.
 */
export type DashboardGranularity = 'day' | 'week' | 'month';

/** Au-delà, on passe du jour à la semaine, puis de la semaine au mois. */
const DAY_GRANULARITY_MAX_DAYS = 31;
const WEEK_GRANULARITY_MAX_DAYS = 182;

/**
 * Le pas qui garde le graphique lisible pour une durée donnée.
 *
 * PUR et partagé : le serveur agrège avec, l'écran étiquette avec, et un test
 * les tient d'accord. `spanDays` est le nombre de jours INCLUS de la fenêtre
 * (du 1er au 31 = 31 jours).
 */
export function granularityForSpan(spanDays: number): DashboardGranularity {
  if (spanDays <= DAY_GRANULARITY_MAX_DAYS) return 'day';
  if (spanDays <= WEEK_GRANULARITY_MAX_DAYS) return 'week';
  return 'month';
}

/**
 * La fenêtre RÉSOLUE, telle que l'API la renvoie et que l'écran l'affiche.
 *
 * `from` et `to` sont des jours `AAAA-MM-JJ`, bornes INCLUSES, dans le fuseau du
 * tenant. L'écran ne recalcule aucune date : il lit celles-ci, et écrit le
 * sous-titre avec.
 */
export interface ResolvedDashboardWindow {
  period: DashboardPeriod;
  from: string;
  to: string;
  granularity: DashboardGranularity;
  /**
   * Les INSTANTS UTC des bornes — basse incluse, haute exclusive — tels que le
   * serveur les a comparés à `createdAt` (chantier « Dashboard de pilotage »,
   * lot G). La recette compte en SQL avec eux, et ne refait aucun calcul de
   * fuseau en base : le Postgres de production et le Node de l'API n'ont pas
   * la même table des fuseaux (Casablanca à +01 pour l'un, GMT pour l'autre
   * depuis le 20/09/2026), et seul le serveur fait foi.
   */
  startAt?: string;
  endAt?: string;
}

/**
 * Un point de la série temporelle, quel que soit le pas.
 *
 * `date` est le premier jour du seau (`AAAA-MM-JJ`) : le début de la journée,
 * de la semaine ou du mois. L'écran l'étiquette selon `granularity`.
 */
export interface DashboardBucketKey {
  date: string;
}
