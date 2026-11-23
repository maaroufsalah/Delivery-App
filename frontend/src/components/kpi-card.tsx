import type { LucideIcon } from 'lucide-react';
import { ArrowDownRight, ArrowUpRight, Minus } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Link } from '@/i18n/navigation';
import { cn } from '@/lib/utils';

/**
 * L'ÉVOLUTION d'une carte — chantier « Dashboard de pilotage ».
 *
 * Déjà FORMATÉE : la carte ne connaît ni la locale ni le sens métier. C'est
 * l'appelant qui dit si « +12 % » est une bonne nouvelle (`positive`) — une
 * hausse des retours ne l'est pas — et la carte ne fait que colorer. `text`
 * vide : rien n'est affiché (pas de période de comparaison).
 */
export interface KpiTrend {
  /** `+12 %`, `−3 %`, `=`… */
  text: string;
  direction: 'up' | 'down' | 'flat';
  /** `null` : neutre (ni vert ni rouge). */
  positive: boolean | null;
  /** Lu par l'attribut `title` : « vs 01/02 – 18/02 ». */
  hint?: string;
}

/**
 * Carte KPI de tête de page. Une seule variante, déclinée par la couleur
 * d'accent passée en `tone` — les pages ne dessinent pas leurs propres cartes.
 *
 * `href` rend la carte CLIQUABLE (drill-down, spec § 15) : c'est un lien, pas
 * un bouton, pour qu'il s'ouvre dans un nouvel onglet et se partage.
 */
export interface KpiCardProps {
  label: string;
  value: string;
  hint?: string;
  icon: LucideIcon;
  /** Couleur d'accent (hex ou var CSS). Par défaut : la primaire de la marque. */
  tone?: string;
  trend?: KpiTrend;
  className?: string;
  /** La liste détaillée que la carte ouvre, filtrée comme elle. */
  href?: string;
}

export function KpiCard({
  label,
  value,
  hint,
  icon: Icon,
  tone,
  trend,
  className,
  href,
}: KpiCardProps) {
  const accent = tone ?? 'var(--primary)';

  const card = (
    <Card
      className={cn(
        // Sous 1700 px, la carte se resserre (chantier « Affichage », point 5).
        'dense:p-3 gap-0 p-4',
        href && 'hover:border-primary/40 h-full transition-colors',
        className,
      )}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-muted-foreground truncate text-xs font-medium tracking-wide uppercase">
            {label}
          </p>
          <p className="dense:mt-1 dense:text-xl mt-1.5 text-2xl font-semibold tracking-tight tabular-nums">
            {value}
          </p>
          <div className="mt-0.5 flex min-w-0 items-center gap-2">
            {trend && trend.text ? <TrendBadge trend={trend} /> : null}
            {hint ? <p className="text-muted-foreground truncate text-xs">{hint}</p> : null}
          </div>
        </div>
        <span
          className="dense:size-8 flex size-9 shrink-0 items-center justify-center rounded-lg"
          style={{
            backgroundColor: `color-mix(in oklab, ${accent} 14%, transparent)`,
            color: accent,
          }}
        >
          <Icon className="size-4.5" />
        </span>
      </div>
    </Card>
  );

  if (!href) return card;

  return (
    <Link
      href={href}
      className="focus-visible:ring-ring block h-full rounded-xl outline-none focus-visible:ring-2"
    >
      {card}
    </Link>
  );
}

function TrendBadge({ trend }: { trend: KpiTrend }) {
  const Arrow =
    trend.direction === 'up' ? ArrowUpRight : trend.direction === 'down' ? ArrowDownRight : Minus;

  // Les couleurs de statut viennent des tokens, jamais d'une palette Tailwind :
  // le vert est celui des colis livrés, le rouge celui des échecs.
  const color =
    trend.positive === null
      ? 'var(--muted-foreground)'
      : trend.positive
        ? 'var(--chart-4)'
        : 'var(--destructive)';

  return (
    <span
      className="inline-flex shrink-0 items-center gap-0.5 rounded-full px-1.5 py-0.5 text-xs font-medium tabular-nums"
      style={{ color, backgroundColor: `color-mix(in oklab, ${color} 12%, transparent)` }}
      title={trend.hint}
    >
      <Arrow className="size-3" aria-hidden />
      {trend.text}
    </span>
  );
}

export function KpiGrid({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={cn('dense:gap-2 grid grid-cols-2 gap-3 lg:grid-cols-4', className)}>
      {children}
    </div>
  );
}
