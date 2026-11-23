import { Medal } from 'lucide-react';
import { cn } from '@/lib/utils';

/**
 * Le rang d'un classement — chantier « Dashboard de pilotage », spec § 4 et 6.
 *
 * Les trois premiers portent une médaille et la couleur de la marque ; les
 * autres, un nombre. Pas d'or, d'argent ni de bronze codés en dur : une seule
 * teinte, trois intensités, dérivée des tokens du thème.
 */
export function RankBadge({ rank }: { rank: number }) {
  if (rank > 3) {
    return <span className="text-muted-foreground text-sm tabular-nums">{rank}</span>;
  }

  const strength = rank === 1 ? 100 : rank === 2 ? 70 : 45;

  return (
    <span
      className={cn(
        'inline-flex size-7 items-center justify-center rounded-full text-xs font-semibold tabular-nums',
      )}
      style={{
        backgroundColor: `color-mix(in oklab, var(--primary) ${strength}%, var(--muted))`,
        color: rank === 1 ? 'var(--primary-foreground)' : 'var(--foreground)',
      }}
      aria-label={`Top ${rank}`}
    >
      {rank === 1 ? <Medal className="size-3.5" aria-hidden /> : rank}
    </span>
  );
}
