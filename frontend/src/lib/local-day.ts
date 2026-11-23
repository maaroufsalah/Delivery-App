/**
 * LES JOURS DE LA SOCIÉTÉ, côté écran — suites de la recette SpeedyTrans 4
 * (D-135).
 *
 * « Aujourd'hui », « 30 derniers jours », « le mois dernier » se calculaient en
 * UTC (`toISOString().slice(0, 10)`) : à 00 h 30 à Casablanca, aujourd'hui
 * était encore hier, et le 1er du mois, « le mois dernier » reculait de deux
 * mois. Le jour se lit dans le fuseau de la société — celui que next-intl porte
 * déjà (`useTimeZone()` / `getTimeZone()`), un NOM (`Africa/Casablanca`), pour
 * que le passage à UTC+0 pendant le Ramadan soit suivi.
 *
 * Ce module ne dépend d'aucune API serveur : il sert aux composants client
 * comme aux pages serveur.
 */

/** Le repli quand la société n'a pas de fuseau : le Maroc, comme l'API. */
const DEFAULT_TIMEZONE = 'Africa/Casablanca';

/** Le jour local d'un instant, `AAAA-MM-JJ`. */
export function localToday(timeZone: string | undefined, now: Date = new Date()): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: timeZone ?? DEFAULT_TIMEZONE }).format(now);
}

/** Décale un jour `AAAA-MM-JJ` de `n` jours, en calendrier. */
export function shiftDay(day: string, n: number): string {
  const [year, month, date] = day.split('-').map(Number);
  return new Date(Date.UTC(year!, month! - 1, date! + n)).toISOString().slice(0, 10);
}

/** Le jour de la semaine ISO d'un jour `AAAA-MM-JJ` : 0 = lundi, 6 = dimanche. */
export function isoWeekday(day: string): number {
  const [year, month, date] = day.split('-').map(Number);
  return (new Date(Date.UTC(year!, month! - 1, date!)).getUTCDay() + 6) % 7;
}

/** Le mois PRÉCÉDENT celui d'un jour `AAAA-MM-JJ`, en bornes incluses. */
export function previousMonth(day: string): { start: string; end: string } {
  const [year, month] = day.split('-').map(Number);
  const start = new Date(Date.UTC(year!, month! - 2, 1)).toISOString().slice(0, 10);
  const end = new Date(Date.UTC(year!, month! - 1, 0)).toISOString().slice(0, 10);
  return { start, end };
}
