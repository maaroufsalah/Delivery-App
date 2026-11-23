import { z } from 'zod';
import { buildThemeTokens, contrastRatio, isHexColor, MIN_CONTRAST, normalizeHex } from './themes';
import type { ThemeOverrides } from './theme-overrides';

/**
 * Palettes de marque prêtes à l'emploi — B-10.
 *
 * **Ce que c'est.** Un couple de couleurs (primaire, secondaire), c'est-à-dire
 * exactement ce que `buildThemeTokens` prend en entrée. Une palette ne décide
 * de rien d'autre : tout le jeu de tokens en est dérivé, comme pour une couleur
 * saisie à la main.
 *
 * **Ce que ce n'est PAS.** À ne pas confondre avec `THEME_PRESETS`
 * (`theme-overrides.ts`), qui sont des préréglages de ZONES — barre latérale,
 * en-tête, tableaux — du thème avancé (D-033). Les deux étages restent
 * indépendants : choisir une palette ne touche pas aux zones, sauf demande
 * explicite.
 *
 * **Où elles vivent.** Cette liste est la valeur par DÉFAUT, posée au seed dans
 * `Setting theme.presets` (D-019, règle n°3 : la valeur par défaut vit dans le
 * seed, le code lit le réglage). L'interface lit la base, jamais ce fichier —
 * c'est ce qui rend la liste extensible sans redéploiement. Le fichier n'est la
 * source que pour le seed et pour les tests.
 */

export interface BrandPalette {
  /** Clé stable, indépendante du libellé affiché. */
  key: string;
  label: string;
  primary: string;
  secondary: string;
}

/**
 * Les onze palettes livrées.
 *
 * `orange` est le défaut : c'est la marque du premier client de la plateforme,
 * et celle de toutes les installations existantes. Elle figure en tête et ne se
 * retire pas.
 *
 * **Les libellés ne nomment jamais un client.** La grille des palettes est vue
 * par TOUS les tenants depuis C8 (D-048) : y lire le nom d'une autre société
 * lui apprendrait qu'elle est là, et nomme une couleur d'après quelqu'un qui
 * n'est pas soi. Ce sont donc des noms de couleur, et rien d'autre.
 *
 * Chacune est vérifiée par `brand-palettes.spec.ts` sur les trois surfaces que
 * l'œil rencontre en premier — bouton, en-tête de tableau, barre latérale. Une
 * palette qui tombe sous 4,5:1 fait rougir les tests, et ne se livre pas.
 */
export const DEFAULT_BRAND_PALETTES: readonly BrandPalette[] = [
  { key: 'orange', label: 'Orange', primary: '#F05A28', secondary: '#1F1F1F' },
  { key: 'cobalt', label: 'Bleu cobalt', primary: '#0F4C81', secondary: '#0B1F3A' },
  { key: 'navy', label: 'Bleu nuit', primary: '#1E3A5F', secondary: '#0E1B2E' },
  { key: 'turquoise', label: 'Turquoise', primary: '#0AB4B0', secondary: '#0F3D3E' },
  { key: 'emerald', label: 'Vert émeraude', primary: '#178F5F', secondary: '#0F2B21' },
  { key: 'olive', label: 'Vert olive', primary: '#5B7F2B', secondary: '#1F2A14' },
  { key: 'carmine', label: 'Rouge carmin', primary: '#C8102E', secondary: '#1A1A1A' },
  { key: 'coral', label: 'Corail', primary: '#E8505B', secondary: '#2A1A1D' },
  { key: 'violet', label: 'Violet', primary: '#6941C6', secondary: '#1F1235' },
  { key: 'amber', label: 'Ambre', primary: '#C98A1B', secondary: '#2B2118' },
  /*
   * Anthracite : la primaire est un gris foncé, donc l'accent dérivé — un
   * mélange de la primaire dans le fond — est un gris très clair, et non une
   * teinte. C'est voulu, et c'est ce que la spec appelle « accent = primaire
   * clair dérivé » : la dérivation le produit déjà, il n'y a rien à surcharger.
   */
  { key: 'graphite', label: 'Anthracite', primary: '#3A3A3A', secondary: '#111111' },
];

/** La palette par défaut — celle des installations existantes. */
export const DEFAULT_BRAND_PALETTE_KEY = 'orange';

export const brandPaletteSchema = z.object({
  key: z
    .string()
    .trim()
    .min(2, 'too-short')
    .max(40)
    .regex(/^[a-z0-9-]+$/, 'invalid-key'),
  label: z.string().trim().min(2, 'too-short').max(60),
  primary: z
    .string()
    .refine((value) => isHexColor(value), 'invalid-color')
    .transform((value) => normalizeHex(value, '#000000')),
  secondary: z
    .string()
    .refine((value) => isHexColor(value), 'invalid-color')
    .transform((value) => normalizeHex(value, '#000000')),
});

export const brandPalettesSchema = z.array(brandPaletteSchema).max(60);

/**
 * Les trois surfaces sur lesquelles une palette se juge.
 *
 * Ce ne sont pas les trente-six tokens : ce sont les trois endroits où un agent
 * lit du texte posé sur une couleur de marque. Si ces trois-là passent, le
 * reste est du neutre, dont le contraste ne dépend pas de la palette.
 */
export interface PaletteContrast {
  /** Le bouton principal : `primary` / `primary-foreground`. */
  button: number;
  /** L'en-tête de tableau : il porte la couleur d'action depuis C1. */
  tableHeader: number;
  /** La barre latérale : `sidebar` / `sidebar-foreground`. */
  sidebar: number;
}

/**
 * Mesure les trois rapports de contraste d'une palette, en mode CLAIR.
 *
 * Le mode sombre n'est pas mesuré ici : ses surfaces sont des neutres fixes
 * (`NEUTRAL_DARK`), et seule la primaire y voyage — sur un fond sombre imposé,
 * où elle est toujours plus lisible qu'en clair.
 */
export function paletteContrast(
  palette: Pick<BrandPalette, 'primary' | 'secondary'>,
): PaletteContrast {
  const { light } = buildThemeTokens({ primary: palette.primary, secondary: palette.secondary });

  return {
    button: contrastRatio(light.primary, light['primary-foreground']),
    tableHeader: contrastRatio(light['table-header'], light['table-header-foreground']),
    sidebar: contrastRatio(light.sidebar, light['sidebar-foreground']),
  };
}

/** Vrai quand les trois surfaces atteignent le seuil AA. */
export function isPaletteReadable(palette: Pick<BrandPalette, 'primary' | 'secondary'>): boolean {
  const ratios = paletteContrast(palette);
  return (
    ratios.button >= MIN_CONTRAST &&
    ratios.tableHeader >= MIN_CONTRAST &&
    ratios.sidebar >= MIN_CONTRAST
  );
}

/**
 * Instantané complet d'une identité visuelle — les deux couleurs ET les zones.
 *
 * C'est l'unité que manipulent « Revenir au standard » et « Annuler la dernière
 * modification » : restaurer la palette sans les zones laisserait une interface
 * mi-repeinte, ce qui n'est le retour à rien du tout.
 */
export interface ThemeSnapshot {
  primaryColor: string;
  secondaryColor: string;
  theme: ThemeOverrides;
}
