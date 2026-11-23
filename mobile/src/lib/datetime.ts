/**
 * LA DATE ET L'HEURE D'UN INSTANT, comme sur le web — chantier « Affichage :
 * factures par jour, heure sur le mobile » (D-254, point b).
 *
 * Le web écrit tout instant `JJ/MM/AAAA HH:MM:SS` (format `stamp`). L'appli
 * mélangeait jour seul, jour et heure sans secondes, et le format du
 * téléphone : un livreur et un agent ne lisaient pas la même heure pour le
 * même scan. Un seul format désormais, partout où un INSTANT s'affiche.
 *
 * Une DATE de calendrier (date d'un chèque, journée de petite caisse) n'est
 * pas un instant : elle garde son format de jour, sans heure.
 */

/** La langue d'affichage des chiffres et du jour, d'après la langue de l'appli. */
function intlLocale(locale: string): string {
  if (locale === 'ar') return 'ar-MA';
  if (locale === 'en') return 'en-GB';
  return 'fr-MA';
}

/** `14/11/2026 18:56:10` — vide pour une valeur absente, la valeur brute si illisible. */
export function formatStamp(
  iso: string | number | Date | null | undefined,
  locale: string,
): string {
  if (iso === null || iso === undefined || iso === '') return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return String(iso);

  return new Intl.DateTimeFormat(intlLocale(locale), {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  }).format(date);
}
