import {
  buildThemeTokens,
  DEFAULT_RADIUS,
  type ThemeOverrides,
  type ThemeTokens,
} from '@delivery/shared';

/**
 * Projette la configuration société en variables CSS, au SSR.
 *
 * UNE seule société par installation (docs/DECISIONS.md D-013) : un seul jeu de
 * tokens est émis, sous `:root` et `.dark`. Il n'y a plus de scope
 * `[data-company]` ni de bascule client — la marque est une configuration, pas
 * une préférence utilisateur.
 *
 * Ce style est émis HORS COUCHE, et c'est ce qui le fait gagner : une
 * déclaration sans couche l'emporte toujours sur une déclaration en couche,
 * quelle que soit la spécificité. Le repli de globals.css vit dans `base`, il
 * ne peut donc pas reprendre la main.
 *
 * Ne pas chercher à gagner par le sélecteur : c'est ce qui avait cassé à
 * l'étape 1bis. Le scope valait `html[data-company="…"]` (0,1,1) et battait le
 * repli `:root` (0,1,0) ; en retirant l'attribut, il est tombé à `html`
 * (0,0,1) et a perdu — toute la marque virait au gris neutre.
 */
function declarations(tokens: ThemeTokens): string {
  const entries = Object.entries(tokens).map(([name, value]) => `--${name}:${value}`);
  return [`--radius:${DEFAULT_RADIUS}`, ...entries].join(';');
}

export function CompanyStyle({
  primaryColor,
  secondaryColor,
  theme,
}: {
  primaryColor: string;
  secondaryColor: string;
  /** Thème avancé, vide dans l'immense majorité des installations (D-033). */
  theme?: ThemeOverrides;
}) {
  const tokens = buildThemeTokens({ primary: primaryColor, secondary: secondaryColor }, theme);

  // `.dark` après `:root` : à spécificité égale, c'est l'ordre qui tranche.
  const css = [`:root{${declarations(tokens.light)}}`, `.dark{${declarations(tokens.dark)}}`].join(
    '',
  );

  return <style id="company-tokens" dangerouslySetInnerHTML={{ __html: css }} />;
}
