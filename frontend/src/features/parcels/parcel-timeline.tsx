'use client';

import { findParcelStatus, type ParcelEvent } from '@delivery/shared';
import { CalendarClock, Paperclip, User } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { statusIcon } from '@/lib/status-icons';

/**
 * Journal du colis — DOMAIN.md §1.11, append-only.
 *
 * Les événements arrivent du plus récent au plus ancien. Le rail vertical est
 * dessiné par une bordure logique (`border-s`) : il passe à droite en RTL sans
 * feuille de style dédiée.
 */
export function ParcelTimeline({
  events,
  showAuthors = true,
}: {
  events: ParcelEvent[];
  /**
   * Le vendeur lit le fil sans ses auteurs — étanchéité vendeur, point 2.
   * L'API ne les lui envoie plus ; sans ce drapeau, chaque ligne afficherait
   * « Automatique », ce qui serait faux.
   */
  showAuthors?: boolean;
}) {
  const t = useTranslations('parcels.detail');
  const tStatus = useTranslations('status');
  const format = useFormatter();

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center justify-between gap-2">
          <span>{t('timeline')}</span>
          <span className="text-muted-foreground text-xs font-normal tabular-nums">
            {t('eventCount', { count: events.length })}
          </span>
        </CardTitle>
      </CardHeader>
      <CardContent>
        {events.length === 0 ? (
          <p className="text-muted-foreground text-sm">{t('noEvents')}</p>
        ) : (
          <ol className="relative flex flex-col">
            {events.map((event, index) => {
              const status = findParcelStatus(event.statusId);
              const Icon = statusIcon(status?.icon ?? 'Package');
              const last = index === events.length - 1;

              return (
                <li key={event.id} className="flex gap-3">
                  {/* Colonne du rail : pastille + trait jusqu'à l'événement suivant. */}
                  <div className="flex flex-col items-center">
                    <span
                      className="flex size-8 shrink-0 items-center justify-center rounded-full border"
                      style={
                        status
                          ? {
                              backgroundColor: `color-mix(in oklab, ${status.color} 14%, transparent)`,
                              borderColor: `color-mix(in oklab, ${status.color} 32%, transparent)`,
                              color: status.color,
                            }
                          : undefined
                      }
                    >
                      <Icon className="size-4" aria-hidden />
                    </span>
                    {last ? null : <span className="bg-border w-px flex-1" aria-hidden />}
                  </div>

                  <div className={last ? 'min-w-0 flex-1 pb-1' : 'min-w-0 flex-1 pb-6'}>
                    <div className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
                      <span className="text-sm font-medium">
                        {/* SC-004 (lot W3, D-238) : une étape BL a son propre libellé chez le vendeur. */}
                        {event.blStep
                          ? t(`blStep.${event.blStep}`)
                          : status
                            ? tStatus(status.key)
                            : `#${event.statusId}`}
                      </span>
                      <time
                        dateTime={event.createdAt}
                        className="text-muted-foreground text-xs tabular-nums"
                      >
                        {format.dateTime(new Date(event.createdAt), 'stamp')}
                      </time>
                    </div>

                    <div className="text-muted-foreground mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
                      {showAuthors ? (
                        <span className="inline-flex items-center gap-1">
                          <User className="size-3" aria-hidden />
                          {event.authorName ?? t('automaticAuthor')}
                        </span>
                      ) : null}
                      {event.scheduledDate ? (
                        <span className="inline-flex items-center gap-1">
                          <CalendarClock className="size-3" aria-hidden />
                          {format.dateTime(new Date(event.scheduledDate), 'short')}
                        </span>
                      ) : null}
                      {event.attachmentName ? (
                        <span className="inline-flex max-w-full items-center gap-1">
                          <Paperclip className="size-3 shrink-0" aria-hidden />
                          <span className="truncate">{event.attachmentName}</span>
                        </span>
                      ) : null}
                    </div>

                    {event.comment ? (
                      <p className="bg-muted/60 mt-2 rounded-md px-3 py-2 text-sm">
                        {event.comment}
                      </p>
                    ) : null}
                  </div>
                </li>
              );
            })}
          </ol>
        )}
      </CardContent>
    </Card>
  );
}
