'use client';

import { notificationCopy, type NotificationItem } from '@delivery/shared';
import { Bell, CheckCheck } from 'lucide-react';
import { useFormatter, useLocale, useTranslations } from 'next-intl';
import { useTransition } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { useRouter } from '@/i18n/navigation';
import {
  markAllNotificationsReadAction,
  markNotificationReadAction,
} from '@/lib/actions/notifications-feed';
import { cn } from '@/lib/utils';

/**
 * LE CENTRE DE NOTIFICATIONS — V2 vendeur bloc 8, spécification §10.
 *
 * **Le texte est rendu ICI, pas stocké.** La base garde le sujet et ses
 * paramètres ; `notificationCopy()` — le même catalogue que le push, depuis le
 * paquet partagé — en fait une phrase dans la langue du LECTEUR. Quelqu'un qui
 * passe l'interface en arabe relit tout son historique en arabe, et une
 * correction de formulation s'applique rétroactivement, sans script de reprise.
 *
 * **Cliquer une ligne la marque lue ET l'ouvre.** Deux gestes pour une
 * intention unique — « je vais voir » — feraient revenir sur l'écran pour
 * acquitter ce qu'on vient de lire.
 */
export function NotificationFeedList({
  items,
  unread,
}: {
  items: NotificationItem[];
  unread: number;
}) {
  const t = useTranslations('notificationCenter');
  const locale = useLocale();
  const format = useFormatter();
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  /** Là où une notification mène, quand elle mène quelque part. */
  const hrefOf = (data: Record<string, string>): string | null => {
    if (data.parcelCode) return `/parcels/${encodeURIComponent(data.parcelCode)}`;
    if (data.claimCode) return `/claims/${encodeURIComponent(data.claimCode)}`;
    return null;
  };

  const openItem = (item: NotificationItem) => {
    const href = hrefOf(item.data);

    startTransition(async () => {
      if (!item.readAt) {
        const result = await markNotificationReadAction(item.id);
        if (!result.ok) toast.error(result.error);
      }

      if (href) router.push(href);
      else router.refresh();
    });
  };

  return (
    <div className="flex flex-col gap-4">
      {unread > 0 ? (
        <div className="flex items-center gap-3">
          <span className="text-sm font-medium">{t('unread', { count: String(unread) })}</span>
          <span className="flex-1" />
          <Button
            size="sm"
            variant="outline"
            disabled={pending}
            onClick={() =>
              startTransition(async () => {
                const result = await markAllNotificationsReadAction();
                if (!result.ok) toast.error(result.error);
                else router.refresh();
              })
            }
          >
            <CheckCheck className="size-4" aria-hidden />
            {t('markAll')}
          </Button>
        </div>
      ) : null}

      {items.length === 0 ? (
        <Card className="items-center gap-2 p-10 text-center">
          <Bell className="text-muted-foreground/60 size-8" aria-hidden />
          <p className="text-sm font-medium">{t('empty')}</p>
          <p className="text-muted-foreground text-xs">{t('emptyHint')}</p>
        </Card>
      ) : (
        <ul className="flex flex-col gap-2">
          {items.map((item) => {
            const copy = notificationCopy(item.topic, locale, item.params);

            return (
              <li key={item.id}>
                <button
                  type="button"
                  onClick={() => openItem(item)}
                  disabled={pending}
                  className={cn(
                    'hover:bg-muted/50 focus-visible:ring-ring flex w-full flex-col items-start gap-0.5 rounded-lg border p-3 text-start outline-none focus-visible:ring-2',
                    /* La bordure marque le NON-LU : une pastille se cherche,
                       une bordure se voit du coin de l'œil en parcourant. */
                    item.readAt ? 'bg-card' : 'border-primary bg-primary/5',
                  )}
                >
                  <span className="text-sm font-semibold">{copy?.title ?? item.topic}</span>
                  <span className="text-muted-foreground text-sm">{copy?.body ?? ''}</span>
                  <time className="text-muted-foreground text-xs" dateTime={item.createdAt}>
                    {format.dateTime(new Date(item.createdAt), 'stamp')}
                  </time>
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
