/**
 * Code colis — DOMAIN.md §3.2.
 *
 * Format : `<CODE_VILLE_DEPART><JJ><RRR><AAMM><CODE_VILLE_ARRIVEE>`
 * où `RRR` est le rang du colis du jour sur le couple (ville départ, ville arrivée),
 * sur 3 chiffres. Exemple : `CAS` + `01` + `024` + `2410` + `RAB` → `CAS010242410RAB`.
 *
 * ⚠️ L'exemple `CAS011024RAB` cité par la spec et par DOMAIN.md §3.2 ne comporte
 * que 12 caractères : il omet le rang et inverse AAMM en MMAA. Le format écrit
 * fait foi ici (15 caractères). Voir docs/DECISIONS.md.
 *
 * Dettes legacy à ne pas reproduire (DOMAIN.md §4 n°6) : aucune contrainte
 * d'unicité, aucune transaction, et deux compteurs divergents (web / mobile).
 * En cible, la séquence est attribuée en base.
 */

export interface ParcelCodeParts {
  originCode: string;
  destinationCode: string;
  /** Rang du jour sur le couple de villes, 1-based. */
  rank: number;
  date: Date;
}

const pad = (value: number, length: number) => String(value).padStart(length, '0');

export function buildParcelCode({
  originCode,
  destinationCode,
  rank,
  date,
}: ParcelCodeParts): string {
  const day = pad(date.getDate(), 2);
  const year = pad(date.getFullYear() % 100, 2);
  const month = pad(date.getMonth() + 1, 2);
  return `${originCode.toUpperCase()}${day}${pad(rank, 3)}${year}${month}${destinationCode.toUpperCase()}`;
}

/** Préfixe des colis de retour (legacy `RET-<code>`). */
export function buildReturnCode(code: string): string {
  return `RET-${code}`;
}

/** Préfixe des colis d'échange (legacy `ECH-<code>`). */
export function buildExchangeCode(code: string): string {
  return `ECH-${code}`;
}

const PARCEL_CODE_PATTERN = /^([A-Z]{2,3})(\d{2})(\d{3})(\d{2})(\d{2})([A-Z]{2,3})$/;

/** Relit un code colis. Renvoie `null` si le code ne suit pas le format. */
export function parseParcelCode(code: string): ParcelCodeParts | null {
  const match = PARCEL_CODE_PATTERN.exec(code.toUpperCase());
  if (!match) return null;

  const [, originCode, day, rank, year, month, destinationCode] = match;
  if (!originCode || !day || !rank || !year || !month || !destinationCode) return null;

  return {
    originCode,
    destinationCode,
    rank: Number(rank),
    date: new Date(2000 + Number(year), Number(month) - 1, Number(day)),
  };
}
