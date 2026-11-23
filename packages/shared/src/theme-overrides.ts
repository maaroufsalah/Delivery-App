import { z } from 'zod';
import {
  contrastRatio,
  isHexColor,
  MIN_CONTRAST,
  mixHex,
  normalizeHex,
  readableForeground,
} from './themes';

/**
 * Thème AVANCÉ — les surcharges par zone d'interface.
 *
 * `buildThemeTokens` dérive tout le jeu de tokens des deux couleurs de marque.
 * C'est suffisant dans neuf cas sur dix, et ça reste la voie normale. Mais un
 * client qui arrive avec une charte graphique complète veut poser SA couleur de
 * barre latérale, SON gris d'en-tête de tableau — pas une teinte dérivée de son
 * orange.
 *
 * D'où ce second étage : une surcharge OPTIONNELLE, par zone, appliquée APRÈS
 * la dérivation. Un thème vide ne change rien — c'est la propriété que
 * `themes.spec.ts` vérifie explicitement, parce qu'une refonte du thème qui
 * déplace un pixel chez les clients existants ne serait pas une option.
 *
 * Les zones sont celles que l'œil distingue en entrant dans l'application :
 * la barre latérale, l'en-tête, les tableaux, les boutons, les badges. Pas les
 * trente-six tokens shadcn, que personne ne sait nommer.
 */

/* --- Forme ---------------------------------------------------------------- */

export interface SidebarOverrides {
  background?: string;
  foreground?: string;
  /** Fond de l'entrée survolée ou active. */
  accent?: string;
  border?: string;
}

export interface HeaderOverrides {
  background?: string;
  foreground?: string;
  border?: string;
}

export interface TableOverrides {
  /** Fond de la ligne d'en-tête. */
  headerBackground?: string;
  headerForeground?: string;
  /** Fond de la ligne survolée. */
  rowHover?: string;
  border?: string;
}

export interface ButtonOverrides {
  background?: string;
  foreground?: string;
}

export interface BadgeOverrides {
  background?: string;
  foreground?: string;
}

export interface ThemeSections {
  sidebar?: SidebarOverrides;
  header?: HeaderOverrides;
  table?: TableOverrides;
  button?: ButtonOverrides;
  badge?: BadgeOverrides;
}

export interface ThemeOverrides {
  light?: ThemeSections;
  /** Absent : dérivé du clair. Renseigné : il gagne, tel quel. */
  dark?: ThemeSections;
}

export const THEME_SECTIONS = ['sidebar', 'header', 'table', 'button', 'badge'] as const;
export type ThemeSectionName = (typeof THEME_SECTIONS)[number];

/* --- Validation ----------------------------------------------------------- */

const colorSchema = z
  .string()
  .trim()
  .refine((value) => isHexColor(value), 'invalid-color')
  .transform((value) => normalizeHex(value, '#000000'));

const optionalColor = colorSchema.optional().or(z.literal('').transform(() => undefined));

/**
 * Refuse un couple fond/texte illisible.
 *
 * Le contrôle ne se déclenche que si les DEUX sont saisis : un fond seul voit
 * son texte calculé automatiquement, et ce calcul ne produit jamais un couple
 * sous le seuil.
 */
function assertReadable(
  context: z.RefinementCtx,
  background: string | undefined,
  foreground: string | undefined,
  path: string[],
): void {
  if (!background || !foreground) return;
  if (contrastRatio(background, foreground) >= MIN_CONTRAST) return;

  context.addIssue({
    code: 'custom',
    path,
    message: 'contrast-too-low',
  });
}

const sidebarSchema = z
  .object({
    background: optionalColor,
    foreground: optionalColor,
    accent: optionalColor,
    border: optionalColor,
  })
  .superRefine((value, context) => {
    assertReadable(context, value.background, value.foreground, ['foreground']);
  });

const headerSchema = z
  .object({
    background: optionalColor,
    foreground: optionalColor,
    border: optionalColor,
  })
  .superRefine((value, context) => {
    assertReadable(context, value.background, value.foreground, ['foreground']);
  });

const tableSchema = z
  .object({
    headerBackground: optionalColor,
    headerForeground: optionalColor,
    rowHover: optionalColor,
    border: optionalColor,
  })
  .superRefine((value, context) => {
    assertReadable(context, value.headerBackground, value.headerForeground, ['headerForeground']);
  });

const pairSchema = z
  .object({ background: optionalColor, foreground: optionalColor })
  .superRefine((value, context) => {
    assertReadable(context, value.background, value.foreground, ['foreground']);
  });

const sectionsSchema = z.object({
  sidebar: sidebarSchema.optional(),
  header: headerSchema.optional(),
  table: tableSchema.optional(),
  button: pairSchema.optional(),
  badge: pairSchema.optional(),
});

export const themeOverridesSchema = z.object({
  light: sectionsSchema.optional(),
  dark: sectionsSchema.optional(),
});

export type ThemeOverridesInput = z.input<typeof themeOverridesSchema>;
export type ThemeOverridesPayload = z.output<typeof themeOverridesSchema>;

/* --- Nettoyage ------------------------------------------------------------ */

/**
 * Retire les clés vides, et les sections devenues vides.
 *
 * Un thème `{ light: { sidebar: {} } }` n'est pas un thème : il doit se stocker
 * `{}`, sans quoi « le thème est-il personnalisé ? » deviendrait une question
 * sans réponse fiable — et le test de non-régression ne voudrait plus rien dire.
 */
export function pruneTheme(theme: ThemeOverrides | null | undefined): ThemeOverrides {
  if (!theme) return {};

  const result: ThemeOverrides = {};

  for (const mode of ['light', 'dark'] as const) {
    const sections = theme[mode];
    if (!sections) continue;

    const kept: Record<string, Record<string, string>> = {};

    for (const name of THEME_SECTIONS) {
      const section = sections[name] as Record<string, string | undefined> | undefined;
      if (!section) continue;

      const entries = Object.entries(section).filter(
        (entry): entry is [string, string] => typeof entry[1] === 'string' && entry[1] !== '',
      );

      if (entries.length > 0) kept[name] = Object.fromEntries(entries);
    }

    if (Object.keys(kept).length > 0) result[mode] = kept as ThemeSections;
  }

  return result;
}

export function isThemeEmpty(theme: ThemeOverrides | null | undefined): boolean {
  const pruned = pruneTheme(theme);
  return !pruned.light && !pruned.dark;
}

/* --- Dérivation du mode sombre -------------------------------------------- */

/** Le fond sombre de référence, celui de `NEUTRAL_DARK.background`. */
const DARK_GROUND = '#171717';
const LIGHT_GROUND = '#FFFFFF';

/**
 * Transpose une couleur claire vers le mode sombre.
 *
 * Pas de conversion colorimétrique savante : une surface claire devient une
 * surface sombre de la MÊME teinte en la mélangeant au fond sombre, et un
 * texte sombre devient un texte clair en le mélangeant au blanc. C'est
 * approximatif, et c'est assumé — l'admin qui veut mieux renseigne le mode
 * sombre à la main, ce que l'écran propose.
 */
function toDark(hex: string, role: 'surface' | 'text'): string {
  return role === 'surface' ? mixHex(hex, DARK_GROUND, 0.78) : mixHex(hex, LIGHT_GROUND, 0.7);
}

const ROLES: Record<string, 'surface' | 'text'> = {
  background: 'surface',
  foreground: 'text',
  accent: 'surface',
  border: 'surface',
  headerBackground: 'surface',
  headerForeground: 'text',
  rowHover: 'surface',
};

/**
 * Le jeu sombre effectif : celui qui est saisi, complété par le clair transposé.
 *
 * La complétion est par CLÉ et non par section : un admin qui n'a réglé que le
 * fond de la barre latérale en sombre garde la transposition automatique pour
 * son texte, au lieu de se retrouver avec un texte dérivé de la marque sur un
 * fond choisi à la main.
 */
export function resolveDarkSections(theme: ThemeOverrides): ThemeSections | undefined {
  const pruned = pruneTheme(theme);
  if (!pruned.light && !pruned.dark) return undefined;

  const result: Record<string, Record<string, string>> = {};

  for (const name of THEME_SECTIONS) {
    const light = (pruned.light?.[name] ?? {}) as Record<string, string>;
    const dark = (pruned.dark?.[name] ?? {}) as Record<string, string>;

    const merged: Record<string, string> = {};

    for (const [key, value] of Object.entries(light)) {
      merged[key] = toDark(value, ROLES[key] ?? 'surface');
    }
    // Le sombre saisi écrase la transposition, jamais l'inverse.
    Object.assign(merged, dark);

    if (Object.keys(merged).length > 0) result[name] = merged;
  }

  return Object.keys(result).length > 0 ? (result as ThemeSections) : undefined;
}

/* --- Préréglages ---------------------------------------------------------- */

export interface ThemePreset {
  key: string;
  theme: ThemeOverrides;
}

/**
 * Trois points de départ, pas trois thèmes finis.
 *
 * Ils existent parce qu'un écran de dix sélecteurs de couleur devant un admin
 * qui n'a pas de charte graphique ne produit rien. Un préréglage donne une
 * base cohérente, que l'admin retouche ensuite.
 *
 * Aucun ne touche aux boutons : la couleur d'action reste celle de la marque,
 * c'est le seul point non négociable du white-label.
 */
export const THEME_PRESETS: ThemePreset[] = [
  {
    key: 'slate',
    theme: {
      light: {
        sidebar: {
          background: '#0F172A',
          foreground: '#E2E8F0',
          accent: '#1E293B',
          border: '#1E293B',
        },
        header: { background: '#FFFFFF', foreground: '#0F172A', border: '#E2E8F0' },
        table: { headerBackground: '#F1F5F9', headerForeground: '#475569', rowHover: '#F8FAFC' },
        badge: { background: '#E2E8F0', foreground: '#0F172A' },
      },
    },
  },
  {
    key: 'sand',
    theme: {
      light: {
        sidebar: {
          background: '#44403C',
          foreground: '#F5F5F4',
          accent: '#57534E',
          border: '#57534E',
        },
        header: { background: '#FAFAF9', foreground: '#292524', border: '#E7E5E4' },
        table: { headerBackground: '#F5F5F4', headerForeground: '#57534E', rowHover: '#FAFAF9' },
        badge: { background: '#E7E5E4', foreground: '#292524' },
      },
    },
  },
  {
    key: 'ink',
    theme: {
      light: {
        sidebar: {
          background: '#111111',
          foreground: '#FAFAFA',
          accent: '#262626',
          border: '#262626',
        },
        header: { background: '#111111', foreground: '#FAFAFA', border: '#262626' },
        table: { headerBackground: '#F4F4F5', headerForeground: '#3F3F46', rowHover: '#FAFAFA' },
        badge: { background: '#18181B', foreground: '#FAFAFA' },
      },
    },
  },
];

/** Le texte d'une surface surchargée, quand l'admin ne l'a pas choisi. */
export function autoForeground(background: string): string {
  return readableForeground(background);
}
