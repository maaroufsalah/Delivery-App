import { cn } from '@/lib/utils';

/**
 * LE logo d'une société, partout où il s'affiche — dernières corrections,
 * point 5.
 *
 * **Sans fond** (`background` nul — Wimo), il se pose tel quel, au pixel près
 * de ce qui s'affichait avant. **Avec un fond** (SpeedyTrans : un logo BLANC
 * sur transparence), il se pose sur une pastille de cette couleur : on ne
 * recolore jamais un logo, on lui donne le fond qui le rend lisible. La
 * couleur vient de la ligne `Company` (Paramètres > Société), jamais d'une
 * constante — d'où le `style`, seule façon de porter une donnée.
 *
 * `<img>` et non `next/image` : le logo peut être une data URL issue de
 * l'upload admin, que l'optimiseur d'images ne sait pas traiter.
 */
export function CompanyLogo({
  src,
  background,
  size = 'sm',
  className,
}: {
  src: string;
  background: string | null | undefined;
  /** `sm` : barre latérale (32 px). `md` : écrans de connexion (36 px). */
  size?: 'sm' | 'md';
  className?: string;
}) {
  if (!background) {
    return (
      // eslint-disable-next-line @next/next/no-img-element -- data URL possible, cf. en-tête
      <img
        src={src}
        alt=""
        width={size === 'md' ? 36 : 32}
        height={size === 'md' ? 36 : 32}
        className={cn('shrink-0 object-contain', size === 'md' ? 'size-9' : 'size-8', className)}
      />
    );
  }

  return (
    <span
      style={{ backgroundColor: background }}
      className={cn(
        'inline-flex shrink-0 items-center justify-center rounded-md',
        size === 'md' ? 'h-9 px-2' : 'h-8 px-1.5',
        className,
      )}
    >
      {/* eslint-disable-next-line @next/next/no-img-element -- data URL possible, cf. en-tête */}
      <img
        src={src}
        alt=""
        className={cn('w-auto max-w-28 object-contain', size === 'md' ? 'h-6' : 'h-5')}
      />
    </span>
  );
}
