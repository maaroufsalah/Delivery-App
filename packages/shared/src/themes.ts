/**
 * Dérivation des tokens de thème à partir des deux couleurs de marque.
 *
 * DOMAIN.md §1.2 — le branding était codé en dur dans `wimo-theme.css`. Ici la
 * source de vérité est la configuration société (`settings.ts`, écran
 * Paramètres > Société) : ce module ne fait que la projeter en un jeu de tokens
 * shadcn/Tailwind, en clair ET en sombre.
 *
 * Aucune marque n'est déclarée en dur : une installation = une société, dont
 * les deux couleurs sont saisies par l'admin (cf. docs/DECISIONS.md D-013).
 */

import {
  pruneTheme,
  resolveDarkSections,
  type ThemeOverrides,
  type ThemeSections,
} from './theme-overrides';

/** Jeu de tokens consommé par Tailwind v4 et shadcn/ui. */
export interface ThemeTokens {
  background: string;
  foreground: string;
  card: string;
  'card-foreground': string;
  popover: string;
  'popover-foreground': string;
  primary: string;
  'primary-foreground': string;
  secondary: string;
  'secondary-foreground': string;
  muted: string;
  'muted-foreground': string;
  accent: string;
  'accent-foreground': string;
  destructive: string;
  'destructive-foreground': string;
  /*
   * Les deux ÉTATS que `destructive` ne couvre pas — M2/M3.
   *
   * Vert « c'est acté », ambre « ça attend ». Ils ne dépendent pas de la
   * marque : un client dont la couleur primaire est verte ne doit pas voir ses
   * avertissements virer au vert. C'est le même raisonnement que `destructive`,
   * qui est un neutre fixe depuis le début.
   *
   * Ils manquaient, et quatre écrans de M1 les demandaient déjà — le bandeau
   * « en attente d'envoi » s'affichait donc sans fond.
   */
  success: string;
  'success-foreground': string;
  warning: string;
  'warning-foreground': string;
  border: string;
  input: string;
  ring: string;
  'chart-1': string;
  'chart-2': string;
  'chart-3': string;
  'chart-4': string;
  'chart-5': string;
  sidebar: string;
  'sidebar-foreground': string;
  'sidebar-primary': string;
  'sidebar-primary-foreground': string;
  'sidebar-accent': string;
  'sidebar-accent-foreground': string;
  'sidebar-border': string;
  'sidebar-ring': string;

  /*
   * Tokens de ZONE — ajoutés par le thème avancé (C0, D-033).
   *
   * Ils existent même sans surcharge : ils valent alors exactement le token
   * générique qu'ils remplacent, et l'interface est au pixel près celle
   * d'avant. C'est ce qui permet à un client existant de ne rien voir changer,
   * et à un nouveau client de repeindre son en-tête sans toucher au reste.
   */
  header: string;
  'header-foreground': string;
  'header-border': string;
  'table-header': string;
  'table-header-foreground': string;
  'table-row-hover': string;
  'table-border': string;
  badge: string;
  'badge-foreground': string;
}

export interface BrandColors {
  primary: string;
  secondary: string;
}

export const DEFAULT_RADIUS = '0.625rem';

/** Neutres fixes : ils ne dépendent pas de la marque, seulement du mode. */
const NEUTRAL_LIGHT = {
  background: '#FFFFFF',
  foreground: '#1F1F1F',
  muted: '#F5F5F4',
  mutedForeground: '#78716C',
  border: '#E7E5E4',
  destructive: '#DC2626',
  success: '#15803D',
  warning: '#B45309',
} as const;

const NEUTRAL_DARK = {
  background: '#171717',
  foreground: '#FAFAF9',
  card: '#1F1F1F',
  muted: '#292524',
  mutedForeground: '#A8A29E',
  border: '#33302E',
  destructive: '#EF4444',
  success: '#4ADE80',
  warning: '#FBBF24',
  sidebar: '#131313',
  sidebarAccent: '#262626',
  sidebarBorder: '#2A2A2A',
} as const;

const HEX_PATTERN = /^#(?:[0-9a-f]{3}|[0-9a-f]{6})$/i;

export function isHexColor(value: unknown): value is string {
  return typeof value === 'string' && HEX_PATTERN.test(value.trim());
}

/** Normalise `#abc` en `#AABBCC`. Renvoie `fallback` si la valeur est invalide. */
export function normalizeHex(value: string, fallback: string): string {
  if (!isHexColor(value)) return fallback;
  const hex = value.trim().slice(1);
  const full =
    hex.length === 3
      ? hex
          .split('')
          .map((char) => char + char)
          .join('')
      : hex;
  return `#${full.toUpperCase()}`;
}

function channels(hex: string): [number, number, number] {
  const value = normalizeHex(hex, '#000000').slice(1);
  return [
    Number.parseInt(value.slice(0, 2), 16),
    Number.parseInt(value.slice(2, 4), 16),
    Number.parseInt(value.slice(4, 6), 16),
  ];
}

/** Luminance relative WCAG. */
function luminance(hex: string): number {
  const linear = channels(hex).map((channel) => {
    const ratio = channel / 255;
    return ratio <= 0.03928 ? ratio / 12.92 : ((ratio + 0.055) / 1.055) ** 2.4;
  });
  const [r = 0, g = 0, b = 0] = linear;
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/**
 * Seuil WCAG AA pour du texte normal. En dessous, ce n'est pas « moins joli »,
 * c'est illisible pour une partie des agents — et un agent qui ne lit pas le
 * montant à encaisser se trompe de montant.
 *
 * Il vit ICI et non dans `theme-overrides.ts` parce que `readableForeground`
 * s'en sert pour GARANTIR le seuil, et que tout le reste en découle.
 */
export const MIN_CONTRAST = 4.5;

/** Encre par défaut : un noir adouci, moins agressif que le noir pur. */
const INK = NEUTRAL_LIGHT.foreground;

/**
 * Couleur de texte lisible sur `background` — **AA garanti, toujours**.
 *
 * L'ancienne version choisissait entre l'encre et le blanc sur un simple seuil
 * de luminance (0,45). C'était un raccourci, et il produisait des couples sous
 * le seuil : sur l'orange de marque `#F05A28`, il posait du blanc à **3,39:1**,
 * bien en dessous de AA. Le bouton principal de toutes les installations était
 * donc illisible au sens de la norme, depuis l'étape 1.
 *
 * La version actuelle raisonne en contraste, pas en luminance :
 *
 *  1. on regarde lequel des deux pôles — noir pur, blanc pur — contraste le
 *     mieux avec le fond ;
 *  2. côté clair, le blanc est déjà l'extrême, on le rend tel quel ;
 *  3. côté sombre, on préfère l'ENCRE adoucie tant qu'elle passe le seuil, et
 *     on ne l'assombrit que juste ce qu'il faut quand elle ne passe pas.
 *
 * **Pourquoi cela ne peut pas échouer.** Pour une couleur de luminance `L`, le
 * contraste au blanc vaut `1,05 / (L + 0,05)` et celui au noir `(L + 0,05) /
 * 0,05` : leur PRODUIT vaut 21, constant. Le meilleur des deux est donc
 * toujours au moins `√21 ≈ 4,58`, au-dessus du seuil. Quelle que soit la
 * couleur qu'un administrateur saisit, cette fonction rend un texte lisible.
 */
export function readableForeground(background: string): string {
  const onWhite = contrastRatio(background, '#FFFFFF');
  const onBlack = contrastRatio(background, '#000000');

  // Le blanc est son propre extrême : quand il gagne, il passe forcément.
  if (onWhite >= onBlack) return '#FFFFFF';
  if (contrastRatio(background, INK) >= MIN_CONTRAST) return INK;

  // L'encre adoucie ne suffit pas : on l'assombrit par paliers, pas d'un coup.
  // Un pas de 10 % suffit — la boucle s'arrête au premier qui passe.
  for (let step = 1; step <= 10; step += 1) {
    const darker = mixHex(INK, '#000000', step / 10);
    if (contrastRatio(background, darker) >= MIN_CONTRAST) return darker;
  }

  return '#000000';
}

/**
 * Adoucit un texte en le rapprochant de sa surface — sans le rendre illisible.
 *
 * La barre latérale porte un texte volontairement moins contrasté que du blanc
 * pur : c'est ce qui lui donne son aspect posé. Mais un adoucissement aveugle
 * casse sur les surfaces de luminance moyenne — une secondaire `#808080`
 * tombait à **3,49:1**, sous AA, alors que le texte non adouci passait.
 *
 * La règle est donc : on adoucit tant que le seuil tient, et on rend l'encre
 * pleine sinon. Sur les palettes livrées, dont les secondaires sont toutes
 * sombres, l'adoucissement s'applique et l'aspect ne bouge pas.
 */
function dimmedInk(ink: string, surface: string, amount: number): string {
  const dimmed = mixHex(ink, surface, amount);
  return contrastRatio(surface, dimmed) >= MIN_CONTRAST ? dimmed : ink;
}

/**
 * Rapport de contraste WCAG entre deux couleurs, de 1 (identiques) à 21
 * (noir sur blanc). Le seuil AA pour du texte normal est 4,5.
 *
 * Il sert à REFUSER une saisie, pas à la corriger en douce : un admin qui pose
 * du gris clair sur du blanc doit l'apprendre au moment où il le fait, pas le
 * découvrir dans le retour d'un agent qui n'arrive plus à lire les montants.
 */
export function contrastRatio(a: string, b: string): number {
  const first = luminance(a);
  const second = luminance(b);
  const lighter = Math.max(first, second);
  const darker = Math.min(first, second);
  return (lighter + 0.05) / (darker + 0.05);
}

/**
 * Mélange deux couleurs et rend un HEX.
 *
 * `mix()` délègue au navigateur et rend une chaîne `color-mix` — parfait pour
 * une variable CSS, inutilisable pour calculer un contraste. Cette fonction-ci
 * rend une vraie couleur, en RGB. Le mélange n'est donc pas perceptuellement
 * uniforme ; il n'a pas à l'être : il ne sert qu'à transposer une surface vers
 * le mode sombre, où l'admin garde la main s'il veut mieux.
 */
export function mixHex(color: string, into: string, amount: number): string {
  const ratio = Math.min(1, Math.max(0, amount));
  const [r1, g1, b1] = channels(color);
  const [r2, g2, b2] = channels(into);

  const blend = (from: number, to: number) => Math.round(from + (to - from) * ratio);
  const hex = (value: number) => value.toString(16).padStart(2, '0').toUpperCase();

  return `#${hex(blend(r1, r2))}${hex(blend(g1, g2))}${hex(blend(b1, b2))}`;
}

/**
 * Mélange en espace oklab, délégué au navigateur.
 * Garder `color-mix` plutôt que de calculer le mélange ici laisse UNE source de
 * vérité : la couleur saisie par l'admin. Aucune valeur dérivée n'est figée.
 */
function mix(color: string, amount: number, into: string): string {
  return `color-mix(in oklab, ${color} ${amount}%, ${into})`;
}

/**
 * Dérive le jeu de tokens des couleurs de marque, puis pose les surcharges.
 *
 * L'ordre compte : la dérivation D'ABORD, la surcharge ENSUITE. C'est ce qui
 * fait qu'un thème partiel reste cohérent — un admin qui ne règle que sa barre
 * latérale garde des boutons, des graphiques et des badges dérivés de sa
 * marque, au lieu d'un écran à moitié peint.
 */
export function buildThemeTokens(
  brand: BrandColors,
  overrides?: ThemeOverrides | null,
): { light: ThemeTokens; dark: ThemeTokens } {
  const primary = normalizeHex(brand.primary, '#F05A28');
  const secondary = normalizeHex(brand.secondary, '#1F1F1F');
  const onPrimary = readableForeground(primary);
  const onSecondary = readableForeground(secondary);

  const light: ThemeTokens = {
    background: NEUTRAL_LIGHT.background,
    foreground: NEUTRAL_LIGHT.foreground,
    card: NEUTRAL_LIGHT.background,
    'card-foreground': NEUTRAL_LIGHT.foreground,
    popover: NEUTRAL_LIGHT.background,
    'popover-foreground': NEUTRAL_LIGHT.foreground,
    primary,
    'primary-foreground': onPrimary,
    secondary,
    'secondary-foreground': onSecondary,
    muted: NEUTRAL_LIGHT.muted,
    'muted-foreground': NEUTRAL_LIGHT.mutedForeground,
    accent: mix(primary, 12, NEUTRAL_LIGHT.background),
    'accent-foreground': mix(primary, 72, '#000000'),
    destructive: NEUTRAL_LIGHT.destructive,
    'destructive-foreground': '#FFFFFF',
    success: NEUTRAL_LIGHT.success,
    'success-foreground': readableForeground(NEUTRAL_LIGHT.success),
    warning: NEUTRAL_LIGHT.warning,
    'warning-foreground': readableForeground(NEUTRAL_LIGHT.warning),
    border: NEUTRAL_LIGHT.border,
    input: NEUTRAL_LIGHT.border,
    ring: primary,
    'chart-1': primary,
    'chart-2': secondary,
    'chart-3': '#3498DB',
    'chart-4': '#6AB04C',
    'chart-5': '#8C7AE6',
    // La sidebar porte la couleur secondaire : c'est elle qui donne l'identité
    // visuelle de l'installation au premier coup d'œil.
    sidebar: secondary,
    /*
     * `mixHex` et non `mix` : c'est un token de TEXTE, et le contraste d'un
     * texte doit pouvoir se mesurer. Une chaîne `color-mix()` n'est calculable
     * que par le navigateur — la barre latérale serait alors la seule zone que
     * les tests de lisibilité ne sauraient pas juger. L'écart entre un mélange
     * oklab et un mélange RGB à 22 % est imperceptible ; l'écart entre une
     * valeur testée et une valeur non testée ne l'est pas.
     */
    'sidebar-foreground': dimmedInk(onSecondary, secondary, 0.22),
    'sidebar-primary': primary,
    'sidebar-primary-foreground': onPrimary,
    'sidebar-accent': mix(onSecondary, 12, secondary),
    'sidebar-accent-foreground': onSecondary,
    'sidebar-border': mix(onSecondary, 16, secondary),
    'sidebar-ring': primary,

    // Sans surcharge, chaque zone reprend le token générique qu'elle double.
    header: NEUTRAL_LIGHT.background,
    'header-foreground': NEUTRAL_LIGHT.foreground,
    'header-border': NEUTRAL_LIGHT.border,
    // L'en-tête de tableau porte la couleur d'action depuis C1 : le repli est
    // donc `primary`, pas un gris. Ce sont les valeurs EXACTES des classes que
    // `data-table.tsx` posait en dur avant le thème avancé.
    'table-header': primary,
    'table-header-foreground': onPrimary,
    'table-row-hover': mix(primary, 12, NEUTRAL_LIGHT.background),
    'table-border': NEUTRAL_LIGHT.border,
    badge: NEUTRAL_LIGHT.muted,
    'badge-foreground': NEUTRAL_LIGHT.foreground,
  };

  const dark: ThemeTokens = {
    background: NEUTRAL_DARK.background,
    foreground: NEUTRAL_DARK.foreground,
    card: NEUTRAL_DARK.card,
    'card-foreground': NEUTRAL_DARK.foreground,
    popover: NEUTRAL_DARK.card,
    'popover-foreground': NEUTRAL_DARK.foreground,
    primary,
    'primary-foreground': onPrimary,
    // En sombre, `secondary` reste neutre : une secondaire de marque très foncée
    // (le cas par défaut, #1F1F1F) disparaîtrait sur un fond déjà noir.
    secondary: NEUTRAL_DARK.muted,
    'secondary-foreground': NEUTRAL_DARK.foreground,
    muted: NEUTRAL_DARK.muted,
    'muted-foreground': NEUTRAL_DARK.mutedForeground,
    accent: mix(primary, 22, NEUTRAL_DARK.background),
    'accent-foreground': mix(primary, 60, '#FFFFFF'),
    destructive: NEUTRAL_DARK.destructive,
    /*
     * CALCULÉ, et non blanc d'office — trouvé par M4.
     *
     * Le rouge du mode sombre est plus clair que celui du mode clair (`#EF4444`
     * contre `#DC2626`, pour ressortir sur un fond noir). Du blanc dessus ne
     * fait que **3,3:1**, sous le seuil AA — et c'est le bouton « Supprimer »,
     * le toast d'erreur et le badge de refus qui le portaient, sur le web comme
     * sur le téléphone. `readableForeground` choisit ce qui se lit, comme il le
     * fait déjà pour `success` et `warning` (D-046).
     */
    'destructive-foreground': readableForeground(NEUTRAL_DARK.destructive),
    success: NEUTRAL_DARK.success,
    'success-foreground': readableForeground(NEUTRAL_DARK.success),
    warning: NEUTRAL_DARK.warning,
    'warning-foreground': readableForeground(NEUTRAL_DARK.warning),
    border: NEUTRAL_DARK.border,
    input: NEUTRAL_DARK.border,
    ring: primary,
    'chart-1': primary,
    'chart-2': mix(secondary, 60, '#FFFFFF'),
    'chart-3': '#38BDF8',
    'chart-4': '#4ADE80',
    'chart-5': '#A78BFA',
    sidebar: NEUTRAL_DARK.sidebar,
    'sidebar-foreground': '#D6D3D1',
    'sidebar-primary': primary,
    'sidebar-primary-foreground': onPrimary,
    'sidebar-accent': NEUTRAL_DARK.sidebarAccent,
    'sidebar-accent-foreground': NEUTRAL_DARK.foreground,
    'sidebar-border': NEUTRAL_DARK.sidebarBorder,
    'sidebar-ring': primary,

    header: NEUTRAL_DARK.background,
    'header-foreground': NEUTRAL_DARK.foreground,
    'header-border': NEUTRAL_DARK.border,
    'table-header': primary,
    'table-header-foreground': onPrimary,
    'table-row-hover': mix(primary, 22, NEUTRAL_DARK.background),
    'table-border': NEUTRAL_DARK.border,
    badge: NEUTRAL_DARK.muted,
    'badge-foreground': NEUTRAL_DARK.foreground,
  };

  if (!overrides) return { light, dark };

  return {
    light: applySections(light, pruneTheme(overrides).light),
    dark: applySections(dark, resolveDarkSections(overrides)),
  };
}

/**
 * Pose les surcharges d'une zone sur un jeu de tokens déjà dérivé.
 *
 * Deux principes, et ils expliquent tout le reste :
 *  - une surface surchargée SANS son texte fait recalculer le texte
 *    (`readableForeground`) : poser un fond foncé ne doit jamais laisser un
 *    texte foncé par-dessus ;
 *  - une clé absente ne touche à rien. Un thème vide rend le jeu inchangé,
 *    au caractère près — c'est la propriété que vérifie `themes.spec.ts`.
 */
function applySections(tokens: ThemeTokens, sections: ThemeSections | undefined): ThemeTokens {
  if (!sections) return tokens;

  const next: ThemeTokens = { ...tokens };

  const sidebar = sections.sidebar;
  if (sidebar?.background) {
    next.sidebar = sidebar.background;
    next['sidebar-foreground'] = sidebar.foreground ?? readableForeground(sidebar.background);
  } else if (sidebar?.foreground) {
    next['sidebar-foreground'] = sidebar.foreground;
  }
  if (sidebar?.accent) {
    next['sidebar-accent'] = sidebar.accent;
    next['sidebar-accent-foreground'] = readableForeground(sidebar.accent);
  }
  if (sidebar?.border) next['sidebar-border'] = sidebar.border;

  const header = sections.header;
  if (header?.background) {
    next.header = header.background;
    next['header-foreground'] = header.foreground ?? readableForeground(header.background);
  } else if (header?.foreground) {
    next['header-foreground'] = header.foreground;
  }
  if (header?.border) next['header-border'] = header.border;

  const table = sections.table;
  if (table?.headerBackground) {
    next['table-header'] = table.headerBackground;
    next['table-header-foreground'] =
      table.headerForeground ?? readableForeground(table.headerBackground);
  } else if (table?.headerForeground) {
    next['table-header-foreground'] = table.headerForeground;
  }
  if (table?.rowHover) next['table-row-hover'] = table.rowHover;
  if (table?.border) next['table-border'] = table.border;

  const button = sections.button;
  if (button?.background) {
    next.primary = button.background;
    next['primary-foreground'] = button.foreground ?? readableForeground(button.background);
    // L'anneau de focus suit le bouton : c'est la même couleur d'action.
    next.ring = button.background;
  } else if (button?.foreground) {
    next['primary-foreground'] = button.foreground;
  }

  const badge = sections.badge;
  if (badge?.background) {
    next.badge = badge.background;
    next['badge-foreground'] = badge.foreground ?? readableForeground(badge.background);
  } else if (badge?.foreground) {
    next['badge-foreground'] = badge.foreground;
  }

  return next;
}

/* --- Jetons CALCULABLES, pour les plateformes sans moteur CSS — M4 -------- */

/**
 * Le même jeu de jetons, mais dont chaque valeur est une couleur RÉELLE.
 *
 * `buildThemeTokens()` laisse volontairement quelques jetons sous forme de
 * `color-mix(in oklab, …)` : le navigateur mélange mieux que nous, et garder
 * l'expression laisse UNE source de vérité — la couleur saisie par l'admin.
 * React Native n'a pas de moteur CSS : `color-mix()` n'y est pas une couleur,
 * c'est une chaîne qu'aucune vue ne sait peindre, et un dégradé SVG à qui on la
 * donne rend du transparent.
 *
 * Cette fonction ne DÉFINIT rien. Elle n'ajoute aucune couleur, n'en choisit
 * aucune : elle exécute le mélange que le navigateur aurait fait, avec
 * `mixHex`. Le mobile obtient donc exactement la palette du web, dérivée de la
 * même marque par la même fonction — pas un second jeu écrit à la main, qui
 * serait le jour même en retard d'une décision.
 *
 * L'écart entre un mélange oklab et un mélange RGB est imperceptible, et c'est
 * déjà l'arbitrage rendu pour `sidebar-foreground` (voir plus haut) : une
 * valeur mesurable vaut mieux qu'une valeur exacte mais opaque.
 */
export function resolveTokens(tokens: ThemeTokens): ThemeTokens {
  const resolved = {} as Record<string, string>;

  for (const [name, value] of Object.entries(tokens)) {
    resolved[name] = resolveColor(value);
  }

  return resolved as unknown as ThemeTokens;
}

/** `color-mix(in oklab, #AAA 12%, #FFF)` → `#F2F2F2`. Le reste passe tel quel. */
function resolveColor(value: string): string {
  const match = /^color-mix\(in oklab,\s*(\S+)\s+([\d.]+)%,\s*(\S+)\)$/.exec(value.trim());
  if (!match) return value;

  const [, color, amount, into] = match as unknown as [string, string, string, string];

  /*
   * `color-mix(A p%, B)` garde p % de A : `mixHex(A, B, r)` va de A vers B,
   * donc le ratio est l'INVERSE. L'inverser à l'envers donnerait une palette
   * presque juste — le genre d'erreur qu'on ne voit pas, et qui fait un fond
   * accentué trop vif pendant six mois.
   */
  return mixHex(color, into, 1 - Number(amount) / 100);
}

/**
 * Les deux bornes d'un dégradé de marque — M4, l'en-tête de l'accueil livreur.
 *
 * Dérivé, jamais choisi : la couleur primaire du tenant, et la même poussée
 * vers sa secondaire. Un dégradé écrit à la main serait la première couleur en
 * dur de l'application, et la seule que le changement de palette n'atteindrait
 * pas.
 *
 * La poussée est FAIBLE (30 %) : un dégradé se remarque quand il est doux et
 * se subit quand il ne l'est pas. Sur une marque dont les deux couleurs sont
 * proches, il devient un aplat — ce qui est le bon comportement, pas un défaut.
 */
export function brandGradient(tokens: ThemeTokens): { from: string; to: string } {
  const from = resolveColor(tokens.primary);
  const to = mixHex(from, resolveColor(tokens.secondary), 0.3);

  return { from, to };
}
