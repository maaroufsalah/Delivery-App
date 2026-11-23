'use client';

import { type BrandPalette, buildThemeTokens, normalizeHex } from '@delivery/shared';
import { Check, SlidersHorizontal } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useMemo, useState } from 'react';
import { cn } from '@/lib/utils';

/**
 * Grille de palettes — B-10.
 *
 * Elle remplace les deux sélecteurs de couleur, qui supposaient une charte
 * graphique et un œil. Un transporteur qui ouvre l'écran veut choisir « du
 * bleu », pas saisir `#0F4C81`.
 *
 * Chaque carte montre ce que la palette PRODUIT — un bouton, un badge, un
 * en-tête de tableau — et non deux pastilles abstraites : les deux couleurs de
 * marque dérivent une trentaine de tokens, et c'est le résultat qui se choisit.
 *
 * Le survol bascule l'aperçu en SOMBRE. C'est le défaut le plus cher de
 * l'écran précédent : on règle en clair, et on découvre son mode sombre
 * illisible le lendemain, par un agent qui n'ose pas le dire.
 *
 * La dernière carte est « Personnalisé » : elle ne pose aucune couleur, elle
 * révèle les sélecteurs d'origine. Rien n'est retiré, tout est rangé.
 */

/** Aperçu miniature d'un jeu de tokens : bouton, badge, en-tête de tableau. */
function Swatch({
  tokens,
  className,
}: {
  tokens: ReturnType<typeof buildThemeTokens>['light'];
  className?: string;
}) {
  return (
    <div
      className={cn('flex flex-col gap-1.5 rounded-md p-2', className)}
      style={{ backgroundColor: tokens.background }}
      aria-hidden
    >
      {/* En-tête de tableau : il porte la couleur d'action depuis C1. */}
      <div
        className="rounded-sm px-1.5 py-1 text-[9px] font-semibold tracking-wide uppercase"
        style={{
          backgroundColor: tokens['table-header'],
          color: tokens['table-header-foreground'],
        }}
      >
        {'---'}
      </div>

      <div className="flex items-center gap-1.5">
        <span
          className="rounded-sm px-2 py-1 text-[9px] font-medium"
          style={{ backgroundColor: tokens.primary, color: tokens['primary-foreground'] }}
        >
          {'•••'}
        </span>
        <span
          className="rounded-full px-1.5 py-0.5 text-[9px]"
          style={{ backgroundColor: tokens.badge, color: tokens['badge-foreground'] }}
        >
          {'•'}
        </span>
        <span className="ms-auto h-4 w-6 rounded-sm" style={{ backgroundColor: tokens.sidebar }} />
      </div>
    </div>
  );
}

function PaletteCard({
  palette,
  selected,
  onSelect,
}: {
  palette: BrandPalette;
  selected: boolean;
  onSelect: () => void;
}) {
  // Le survol montre le SOMBRE : régler en clair et découvrir son sombre le
  // lendemain est exactement ce que cet écran doit empêcher.
  const [dark, setDark] = useState(false);

  const tokens = useMemo(
    () => buildThemeTokens({ primary: palette.primary, secondary: palette.secondary }),
    [palette.primary, palette.secondary],
  );

  return (
    <button
      type="button"
      onClick={onSelect}
      onMouseEnter={() => setDark(true)}
      onMouseLeave={() => setDark(false)}
      onFocus={() => setDark(true)}
      onBlur={() => setDark(false)}
      aria-pressed={selected}
      className={cn(
        'group relative flex flex-col gap-2 rounded-lg border p-2.5 text-start transition-colors',
        'focus-visible:ring-ring outline-none focus-visible:ring-2',
        selected ? 'border-primary ring-primary/30 ring-2' : 'hover:border-foreground/30',
      )}
    >
      {selected ? (
        <span
          className="bg-primary text-primary-foreground absolute end-2 top-2 flex size-4 items-center justify-center rounded-full"
          aria-hidden
        >
          <Check className="size-3" />
        </span>
      ) : null}

      <div className="flex items-center gap-1.5">
        <span
          className="size-3.5 shrink-0 rounded-full border"
          style={{ backgroundColor: palette.primary }}
          aria-hidden
        />
        <span
          className="size-3.5 shrink-0 rounded-full border"
          style={{ backgroundColor: palette.secondary }}
          aria-hidden
        />
      </div>

      <Swatch tokens={dark ? tokens.dark : tokens.light} className="border" />

      <span className="truncate text-xs font-medium">{palette.label}</span>
    </button>
  );
}

export function PaletteGrid({
  palettes,
  primary,
  secondary,
  custom,
  onSelect,
  onCustom,
}: {
  palettes: BrandPalette[];
  primary: string;
  secondary: string;
  /** Vrai quand l'utilisateur a ouvert les sélecteurs à la main. */
  custom: boolean;
  onSelect: (palette: BrandPalette) => void;
  onCustom: () => void;
}) {
  const t = useTranslations('settings.company.palettes');

  const currentKey = useMemo(() => {
    const current = `${normalizeHex(primary, '')}/${normalizeHex(secondary, '')}`;
    return palettes.find(
      (palette) =>
        `${normalizeHex(palette.primary, '')}/${normalizeHex(palette.secondary, '')}` === current,
    )?.key;
  }, [palettes, primary, secondary]);

  return (
    <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-3 lg:grid-cols-4">
      {palettes.map((palette) => (
        <PaletteCard
          key={palette.key}
          palette={palette}
          // Une palette n'est « choisie » que si les DEUX couleurs courantes
          // correspondent : retoucher l'une d'elles à la main désélectionne, et
          // c'est honnête — ce n'est plus cette palette.
          selected={!custom && currentKey === palette.key}
          onSelect={() => onSelect(palette)}
        />
      ))}

      {/* Dernière carte : elle ne pose aucune couleur, elle ouvre les pickers. */}
      <button
        type="button"
        onClick={onCustom}
        aria-pressed={custom}
        className={cn(
          'flex flex-col items-center justify-center gap-2 rounded-lg border border-dashed p-2.5 transition-colors',
          'focus-visible:ring-ring outline-none focus-visible:ring-2',
          custom ? 'border-primary ring-primary/30 ring-2' : 'hover:border-foreground/30',
        )}
      >
        <SlidersHorizontal className="text-muted-foreground size-5" aria-hidden />
        <span className="text-xs font-medium">{t('custom')}</span>
        {custom || !currentKey ? (
          <span className="flex items-center gap-1.5">
            <span
              className="size-3 rounded-full border"
              style={{ backgroundColor: primary }}
              aria-hidden
            />
            <span
              className="size-3 rounded-full border"
              style={{ backgroundColor: secondary }}
              aria-hidden
            />
          </span>
        ) : null}
      </button>
    </div>
  );
}
