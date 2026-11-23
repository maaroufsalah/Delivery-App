'use client';

import type { AuditLogDetail, JournalEntryDetail } from '@delivery/shared';
import { ExternalLink, Wrench } from 'lucide-react';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Link } from '@/i18n/navigation';
import { technicalGroupAction } from '@/lib/actions/technical';
import { AuditDiff } from './audit-diff';
import { useJournalText } from './use-journal-text';

/**
 * LE DÉTAIL D'UNE LIGNE DU JOURNAL — recette SpeedyTrans 4, point 4 (D-133).
 *
 * Seulement les champs MÉTIER modifiés, avant → après, libellés traduits et
 * valeurs formatées ; puis ce qui situe le geste : motif, source, agence,
 * adresse IP (ici seulement, jamais dans la liste), autres objets touchés.
 */
export function JournalDetail({
  entry,
  technical = false,
}: {
  entry: JournalEntryDetail;
  /**
   * « Vue technique » — recette 4, point 5 a (D-134) : les lignes BRUTES de la
   * requête, pour Nocido en « se connecter en tant que ». L'API refuse tout
   * autre compte ; ce drapeau ne fait que ne pas proposer un bouton voué au 403.
   */
  technical?: boolean;
}) {
  const { t, tx, value, field, object } = useJournalText();
  const [raw, setRaw] = useState<AuditLogDetail[] | null>(null);
  const [loading, startTransition] = useTransition();

  const openTechnical = () =>
    startTransition(async () => {
      const result = await technicalGroupAction(entry.id);
      if (result.ok) setRaw(result.data);
      else toast.error(result.error);
    });

  const facts: Array<[string, string | null]> = [
    [t('detail.reason'), entry.reason],
    [t('detail.source'), tx(`source.${entry.source}`)],
    [t('detail.agency'), entry.agency],
    [t('detail.email'), entry.user?.email ?? null],
    [t('detail.ip'), entry.ip],
  ];

  return (
    <div className="flex flex-col gap-5 text-sm">
      {entry.object.href ? (
        <Link
          href={entry.object.href as never}
          className="text-primary inline-flex items-center gap-1.5 self-start font-medium hover:underline"
        >
          <ExternalLink className="size-3.5" aria-hidden />
          {t('detail.open')}
        </Link>
      ) : null}

      <section className="flex flex-col gap-2">
        <h3 className="font-semibold">{t('detail.changes')}</h3>
        {entry.changes.length === 0 ? (
          <p className="text-muted-foreground">{t('detail.noChanges')}</p>
        ) : (
          <div className="flex flex-col">
            <div className="text-muted-foreground hidden grid-cols-[10rem_minmax(0,1fr)_minmax(0,1fr)] gap-3 pb-1 text-xs font-medium sm:grid">
              <span>{t('detail.field')}</span>
              <span>{t('detail.before')}</span>
              <span>{t('detail.after')}</span>
            </div>
            {entry.changes.map((change) => (
              <div
                key={change.field}
                className="border-border grid grid-cols-[minmax(0,1fr)] gap-1 border-t py-2 sm:grid-cols-[10rem_minmax(0,1fr)_minmax(0,1fr)] sm:gap-3"
              >
                <span className="font-medium">{field(change)}</span>
                <span className="text-muted-foreground break-words">
                  {change.before === null ? '—' : value(change.before)}
                </span>
                <span className="break-words">{value(change.after)}</span>
              </div>
            ))}
          </div>
        )}
      </section>

      <dl className="grid grid-cols-[minmax(0,1fr)] gap-x-3 gap-y-2 sm:grid-cols-[10rem_minmax(0,1fr)]">
        {facts
          .filter(([, fact]) => fact)
          .map(([label, fact]) => (
            <div key={label} className="contents">
              <dt className="text-muted-foreground">{label}</dt>
              <dd className="break-words">{fact}</dd>
            </div>
          ))}
      </dl>

      {entry.related.length > 0 ? (
        <section className="flex flex-col gap-2">
          <h3 className="font-semibold">{t('detail.related')}</h3>
          <ul className="flex flex-wrap gap-2">
            {entry.related.map((related) => (
              <li key={`${related.type}-${related.reference}`}>
                {related.href ? (
                  <Link
                    href={related.href as never}
                    className="bg-muted hover:bg-accent rounded-md px-2 py-1 text-xs"
                  >
                    {object(related)}
                  </Link>
                ) : (
                  <span className="bg-muted rounded-md px-2 py-1 text-xs">{object(related)}</span>
                )}
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {technical ? (
        <section className="flex flex-col gap-3 border-t pt-4">
          {raw === null ? (
            <Button
              type="button"
              size="sm"
              variant="outline"
              className="gap-1.5 self-start"
              onClick={openTechnical}
              disabled={loading}
            >
              <Wrench className="size-3.5" aria-hidden />
              {t('detail.technical')}
            </Button>
          ) : (
            <>
              <h3 className="font-semibold">{t('detail.technical')}</h3>
              {raw.map((row) => (
                <div key={row.id} className="flex flex-col gap-2 rounded-md border p-3">
                  <p className="font-mono text-xs">
                    {row.entity} {row.entityId} · {row.action} · {row.requestId ?? '—'}
                  </p>
                  <AuditDiff entry={row} />
                </div>
              ))}
            </>
          )}
        </section>
      ) : null}
    </div>
  );
}
