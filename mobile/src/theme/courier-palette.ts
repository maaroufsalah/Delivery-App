import type { ThemeTokens } from '@delivery/shared';

/**
 * LA PALETTE DE L'ACCUEIL LIVREUR — chantier « Accueil et menu bas livreur
 * simplifiés », `docs/design/livreur-accueil-v2.md`.
 *
 * **Ce qui vient du tenant, et ce qui n'en vient pas.** L'accent — en-tête,
 * bouton Scanner, avatar — est la couleur PRIMAIRE du tenant, lue par
 * `useTokens()` : rien de la marque n'est écrit ici (CLAUDE.md, règle 3). Les
 * neutres de la maquette validée (fond `#F6F3EE`, texte `#1D1B19`, secondaire
 * `#6B6560`) ne sont pas une marque : c'est le papier sur lequel toutes les
 * marques se posent, et la maquette les fixe. Ils ne valent qu'en mode CLAIR ;
 * en sombre, ce sont les jetons du thème qui font le fond et le texte, pour
 * que l'accueil suive le réglage de l'application comme les autres écrans.
 *
 * Contrastes (WCAG AA, ≥ 4,5:1) : `#1D1B19` sur `#F6F3EE` ≈ 15:1 ; `#6B6560`
 * sur `#F6F3EE` ≈ 4,9:1 et sur blanc ≈ 5,3:1.
 *
 * **Les six pastilles** sont des couleurs de CATÉGORIE, pas de statut : elles
 * distinguent six portes d'entrée, et la maquette les nomme (orange clair,
 * bleu, vert, violet, jaune, turquoise). Chacune existe en deux versions,
 * claire (teinte pâle, icône foncée) et sombre (icône claire, teinte à 25 %),
 * pour rester lisible dans les deux modes.
 */
export type Scheme = 'light' | 'dark';

export interface CourierSurface {
  /** Fond de page. */
  background: string;
  /** Fond des cartes et tuiles. */
  card: string;
  /** Texte principal. */
  text: string;
  /** Texte secondaire : sous-titres, libellés des chiffres. */
  muted: string;
  /** Séparateurs fins. */
  border: string;
}

export function courierSurface(scheme: Scheme, tokens: ThemeTokens): CourierSurface {
  if (scheme === 'dark') {
    return {
      background: tokens.background,
      card: tokens.card,
      text: tokens.foreground,
      muted: tokens['muted-foreground'],
      border: tokens.border,
    };
  }

  return {
    background: '#F6F3EE',
    card: '#FFFFFF',
    text: '#1D1B19',
    muted: '#6B6560',
    border: '#EAE5DD',
  };
}

export type TileKey =
  'pickups' | 'deliveries' | 'stock' | 'parcels' | 'returns' | 'remittances' | 'stats';

interface Pastille {
  /** Fond de la pastille. */
  tint: string;
  /** Couleur de l'icône posée dessus. */
  icon: string;
}

const LIGHT: Record<TileKey, Pastille> = {
  pickups: { tint: '#FFE4CC', icon: '#B45309' }, // orange clair
  deliveries: { tint: '#DCEBFF', icon: '#1D4ED8' }, // bleu
  stock: { tint: '#FFE0EA', icon: '#BE185D' }, // rose
  parcels: { tint: '#DCF5E3', icon: '#15803D' }, // vert
  returns: { tint: '#EBE0FF', icon: '#6D28D9' }, // violet
  remittances: { tint: '#FFF1C2', icon: '#A16207' }, // jaune
  stats: { tint: '#CCF3F1', icon: '#0F766E' }, // turquoise
};

const DARK: Record<TileKey, Pastille> = {
  pickups: { tint: '#B4530940', icon: '#FDBA74' },
  deliveries: { tint: '#1D4ED840', icon: '#93C5FD' },
  stock: { tint: '#BE185D40', icon: '#F9A8D4' },
  parcels: { tint: '#15803D40', icon: '#86EFAC' },
  returns: { tint: '#6D28D940', icon: '#C4B5FD' },
  remittances: { tint: '#A1620740', icon: '#FDE68A' },
  stats: { tint: '#0F766E40', icon: '#5EEAD4' },
};

export function tilePastille(key: TileKey, scheme: Scheme): Pastille {
  return (scheme === 'dark' ? DARK : LIGHT)[key];
}

/**
 * Les initiales d'un nom, pour l'avatar : première lettre du prénom et du
 * nom, en majuscules. « Abdo Iziki » → « AI » ; un seul mot → sa première
 * lettre ; rien → « ? ».
 */
export function initialsOf(firstName?: string | null, lastName?: string | null): string {
  const letters = [firstName, lastName]
    .map((part) => (part ?? '').trim().charAt(0).toUpperCase())
    .filter(Boolean);
  return letters.length > 0 ? letters.join('') : '?';
}

/** L'ombre légère des cartes et du bouton central — iOS et Android. */
export const SOFT_SHADOW = {
  shadowColor: '#1D1B19',
  shadowOpacity: 0.08,
  shadowRadius: 12,
  shadowOffset: { width: 0, height: 4 },
  elevation: 3,
} as const;
