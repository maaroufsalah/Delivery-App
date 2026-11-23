'use client';

import type { AuditLogDetail } from '@delivery/shared';
import { useTranslations } from 'next-intl';
import { ScrollArea } from '@/components/ui/scroll-area';
import { cn } from '@/lib/utils';

/**
 * L'avant et l'après, CÔTE À CÔTE — C11 bloc 5, le cahier des charges le
 * demande dans ces termes.
 *
 * Trois choix, et ils tiennent tous à la même idée : un journal se lit vite ou
 * ne se lit pas.
 *
 *  1. **Les champs CHANGÉS d'abord**, en tête et surlignés. Sur un colis à
 *     quarante colonnes, la ligne modifiée se perd dans un objet complet.
 *  2. **Le reste ensuite**, pour le contexte — qui était ce colis au moment du
 *     geste — mais en gris et sans surlignage.
 *  3. **Côte à côte sous `sm`, empilé en dessous.** Deux colonnes de JSON sur
 *     un téléphone ne sont lisibles ni l'une ni l'autre.
 */
export function AuditDiff({ entry, pending }: { entry: AuditLogDetail; pending?: boolean }) {
  const t = useTranslations('settings.journal');

  const before = entry.before ?? {};
  const after = entry.after ?? {};

  const changed = new Set(entry.diff);
  const keys = [...new Set([...Object.keys(before), ...Object.keys(after)])].sort((a, b) => {
    // Les champs changés en tête : c'est ce qu'on est venu voir.
    if (changed.has(a) !== changed.has(b)) return changed.has(a) ? -1 : 1;
    return a.localeCompare(b);
  });

  if (keys.length === 0) {
    return <p className="text-muted-foreground py-6 text-center text-sm">{t('noDetail')}</p>;
  }

  return (
    <div className={cn('flex flex-col gap-3', pending && 'opacity-60')}>
      <div className="text-muted-foreground grid grid-cols-[minmax(0,1fr)] gap-3 text-xs font-medium sm:grid-cols-[10rem_minmax(0,1fr)_minmax(0,1fr)]">
        <span className="hidden sm:block">{t('field')}</span>
        <span className="hidden sm:block">{t('before')}</span>
        <span className="hidden sm:block">{t('after')}</span>
      </div>

      <ScrollArea className="max-h-[60vh]">
        <ul className="divide-border divide-y text-xs">
          {keys.map((key) => {
            const moved = changed.has(key);

            return (
              <li
                key={key}
                className={cn(
                  'grid grid-cols-[minmax(0,1fr)] gap-1 py-2 sm:grid-cols-[10rem_minmax(0,1fr)_minmax(0,1fr)] sm:gap-3',
                  moved ? 'font-medium' : 'text-muted-foreground',
                )}
              >
                <span className="truncate">{key}</span>

                <Value value={before[key]} tone={moved ? 'before' : 'quiet'} />
                <Value value={after[key]} tone={moved ? 'after' : 'quiet'} />
              </li>
            );
          })}
        </ul>
      </ScrollArea>

      {entry.requestId ? (
        <p className="text-muted-foreground border-border border-t pt-2 text-xs">
          {t('requestId', { id: entry.requestId })}
        </p>
      ) : null}
    </div>
  );
}

/**
 * Une valeur de journal, rendue lisible.
 *
 * Les couleurs viennent des tokens de statut, jamais d'une palette Tailwind :
 * la marque d'un client peut être rouge, et un « avant » teinté en rouge de
 * marque ne se distinguerait plus d'une suppression.
 */
function Value({ value, tone }: { value: unknown; tone: 'before' | 'after' | 'quiet' }) {
  const text =
    value === null || value === undefined
      ? '-'
      : typeof value === 'object'
        ? JSON.stringify(value)
        : String(value);

  return (
    <span
      className={cn(
        'min-w-0 break-words',
        tone === 'before' && 'text-destructive line-through decoration-1',
        tone === 'after' && 'text-foreground',
      )}
      style={tone === 'after' ? { color: 'var(--chart-4)' } : undefined}
    >
      {text}
    </span>
  );
}
