/**
 * « LES PLUS RÉCENTS EN PREMIER », à l'intérieur d'un bon ou d'un circuit —
 * chantier « Tri par défaut » (11/11/2026).
 *
 * Le tri se fait ICI, à l'écran, et non dans l'API : le PDF du document lit le
 * même détail, et son ordre ne doit pas bouger (l'ordre du sac d'une feuille de
 * route, l'ordre d'ajout d'un bon de retour).
 *
 * La date est celle de la création du COLIS : les lignes d'un bon ou d'un
 * circuit n'en portent pas d'autre. Une ligne sans date (API plus ancienne)
 * garde sa place d'origine, après les datées ; à date égale — un import — c'est
 * l'ordre du serveur qui départage (le tri est stable).
 */
export function newestFirst<T extends { parcelCreatedAt?: string | null }>(
  lines: readonly T[],
): T[] {
  return [...lines].sort((a, b) => {
    const left = a.parcelCreatedAt ?? '';
    const right = b.parcelCreatedAt ?? '';
    // ISO 8601 en UTC : l'ordre des chaînes est celui des dates.
    return left === right ? 0 : left < right ? 1 : -1;
  });
}
