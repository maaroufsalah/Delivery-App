import { MoreHorizontal } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { cn } from '@/lib/utils';

/**
 * Vocabulaire de cellules commun à toutes les tables.
 * Référence de design : libellé fort + sous-libellé gris, badges arrondis,
 * actions révélées au survol de la ligne.
 */

/** Libellé principal + sous-libellé discret sur une seconde ligne. */
export function CellStack({
  label,
  sub,
  className,
  mono,
}: {
  label: React.ReactNode;
  sub?: React.ReactNode;
  className?: string;
  mono?: boolean;
}) {
  return (
    <div className={cn('flex min-w-0 flex-col gap-0.5', className)}>
      <span
        className={cn(
          'text-foreground truncate text-sm font-medium',
          mono && 'font-mono text-[13px] tracking-tight',
        )}
      >
        {label}
      </span>
      {sub ? <span className="text-muted-foreground truncate text-xs">{sub}</span> : null}
    </div>
  );
}

/** Badge arrondi coloré à partir d'une couleur arbitraire (statuts métier). */
export function CellBadge({
  label,
  color,
  icon: Icon,
  className,
}: {
  label: React.ReactNode;
  /** Couleur hexadécimale. Le fond est dérivé par transparence, le texte garde la teinte. */
  color?: string;
  icon?: React.ComponentType<{ className?: string }>;
  className?: string;
}) {
  return (
    <span
      className={cn(
        'inline-flex max-w-full items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium',
        color ? 'border' : 'bg-muted text-muted-foreground border',
        className,
      )}
      style={
        color
          ? {
              // color-mix garde une seule source de vérité : la couleur du référentiel.
              backgroundColor: `color-mix(in oklab, ${color} 14%, transparent)`,
              borderColor: `color-mix(in oklab, ${color} 32%, transparent)`,
              color,
            }
          : undefined
      }
    >
      {Icon ? <Icon className="size-3.5 shrink-0" /> : null}
      <span className="truncate">{label}</span>
    </span>
  );
}

/** Valeur numérique ou monétaire, alignée et tabulée. */
export function CellNumber({
  value,
  sub,
  className,
}: {
  value: React.ReactNode;
  sub?: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={cn('flex flex-col items-end gap-0.5 tabular-nums', className)}>
      <span className="text-sm font-medium">{value}</span>
      {sub ? <span className="text-muted-foreground text-xs">{sub}</span> : null}
    </div>
  );
}

export function CellMuted({ children }: { children: React.ReactNode }) {
  return <span className="text-muted-foreground text-sm">{children}</span>;
}

/** Conteneur d'actions de ligne — le menu reste accessible au clavier. */
export function RowActions({ children }: { children: React.ReactNode }) {
  const t = useTranslations('common');

  return (
    <div className="flex justify-end">
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            size="icon"
            className="size-8 opacity-100 md:opacity-0 md:group-hover/row:opacity-100 md:focus-visible:opacity-100 md:data-[state=open]:opacity-100"
            aria-label={t('actions')}
          >
            <MoreHorizontal className="size-4" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="min-w-40">
          {children}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
