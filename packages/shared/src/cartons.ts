/**
 * LES CARTONS D'UN DOCUMENT — chantier « Nombre de cartons sur les bons et
 * circuits » (nuit du 11/11/2026).
 *
 * Un colis de Client Pro peut partir en plusieurs cartons sous un seul code
 * (`parcels.quantity`, lot G, D-208). Les bons et les circuits n'en disaient
 * rien : l'agent comptait trois lignes et chargeait six cartons.
 *
 * **Une ligne reste un colis.** Ces fonctions ne dédoublent rien et ne
 * touchent aucun compteur existant (« X / Y ramassés » compte des colis) :
 * elles disent seulement combien de cartons il y a à manipuler.
 *
 * L'écran et le PDF lisent la même règle, pour qu'un bon ne s'imprime pas avec
 * une colonne que sa fiche n'affiche pas.
 */
export interface CartonLine {
  /** Absente ou nulle (API plus ancienne, ligne historique) : un carton. */
  quantity?: number | null;
}

/** Le nombre de cartons d'UNE ligne — jamais moins d'un, jamais une fraction. */
export function cartonsOf(line: CartonLine): number {
  const quantity = Math.trunc(line.quantity ?? 1);
  return Number.isFinite(quantity) && quantity > 1 ? quantity : 1;
}

/** Le total de cartons d'un document. */
export function cartonTotal(lines: readonly CartonLine[]): number {
  return lines.reduce((sum, line) => sum + cartonsOf(line), 0);
}

/**
 * Faut-il MONTRER les cartons sur ce document ?
 *
 * Seulement s'il porte au moins un colis de plusieurs cartons. Un bon ordinaire
 * — un carton par colis — s'affiche et s'imprime exactement comme avant : la
 * colonne n'y dirait que « 1 » sur chaque ligne.
 */
export function showsCartons(lines: readonly CartonLine[]): boolean {
  return lines.some((line) => cartonsOf(line) > 1);
}

/**
 * L'en-tête de la colonne des codes sur les documents qui portent les cartons
 * (bon de ramassage, bon de dépôt, circuit, bons de retour) — demande de Badr,
 * 11/11/2026 : une ligne y est une EXPÉDITION, qui peut compter plusieurs
 * cartons. Partout ailleurs (menu, statuts, compteurs), le mot reste « colis ».
 */
export const SHIPMENT_COLUMN_FR = 'Expédition';

/**
 * LE TOTAL D'UN DOCUMENT, en français — pour les PDF, qui ne sont pas traduits :
 * « 1 expédition », « 3 expéditions », « 3 expéditions · 7 cartons ».
 *
 * Le MÊME libellé que le document ait des cartons ou non : la seconde moitié
 * n'apparaît que s'il porte un colis de plusieurs cartons (`showsCartons`).
 * `count` est le nombre d'expéditions quand il ne se lit pas sur `lines`
 * (un bon dont le détail compte ses lignes à part).
 */
export function shipmentTotalFr(lines: readonly CartonLine[], count = lines.length): string {
  const shipments = `${count} ${count === 1 ? 'expédition' : 'expéditions'}`;
  if (!showsCartons(lines)) return shipments;

  const cartons = cartonTotal(lines);
  return `${shipments} · ${cartons} ${cartons === 1 ? 'carton' : 'cartons'}`;
}
