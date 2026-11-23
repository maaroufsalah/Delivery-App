'use client';

import type { AnnouncementListItem } from '@delivery/shared';
import { Eye, FileEdit, Megaphone, Plus, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useTransition } from 'react';
import { toast } from 'sonner';
import { CellBadge } from '@/components/data-table/cells';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Link, useRouter } from '@/i18n/navigation';
import { deleteAnnouncementAction } from '@/lib/actions/announcements';

/**
 * Actualités — la liste.
 *
 * Des CARTES et non la `DataTable` : une actualité se reconnaît à son titre et
 * à son chapeau, pas à une ligne de tableau. La règle « une seule table »
 * (CLAUDE.md §2) interdit d'en déclarer une seconde, elle n'oblige pas à
 * tabuler ce qui n'est pas tabulaire.
 *
 * Le staff voit aussi les brouillons, signalés comme tels. Les autres comptes
 * ne reçoivent que ce qui les concerne — l'API a déjà filtré.
 */
export function NewsScreen({
  items,
  canWrite,
}: {
  items: AnnouncementListItem[];
  canWrite: boolean;
}) {
  const t = useTranslations('referential.news');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  const remove = (id: string, title: string) => {
    startTransition(async () => {
      const result = await deleteAnnouncementAction(id);
      if (!result.ok) {
        toast.error(result.error);
        return;
      }
      toast.success(t('removed', { title }));
      router.refresh();
    });
  };

  if (items.length === 0) {
    return (
      <Card>
        <CardContent className="flex flex-col items-center gap-3 py-16 text-center">
          <Megaphone className="text-muted-foreground/60 size-8" aria-hidden />
          <p className="text-sm font-medium">{t('empty')}</p>
          <p className="text-muted-foreground text-xs">{t('emptyHint')}</p>
          {canWrite ? (
            <Button asChild size="sm" className="gap-1.5">
              <Link href="/news/new">
                <Plus className="size-4" aria-hidden />
                {t('new')}
              </Link>
            </Button>
          ) : null}
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      {canWrite ? (
        <Button asChild size="sm" className="gap-1.5 self-end">
          <Link href="/news/new">
            <Plus className="size-4" aria-hidden />
            {t('new')}
          </Link>
        </Button>
      ) : null}

      <ul className="flex flex-col gap-3">
        {items.map((item) => (
          <li key={item.id}>
            <Card>
              <CardContent className="flex flex-col gap-3 pt-5 sm:flex-row sm:items-start">
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <Link
                      href={`/news/${encodeURIComponent(item.id)}`}
                      className="font-semibold hover:underline"
                    >
                      {item.title}
                    </Link>

                    {item.isPublished ? null : (
                      <CellBadge label={t('draft')} color="var(--muted-foreground)" />
                    )}

                    {item.audience === 'all' ? null : (
                      <CellBadge label={t(`audience.${item.audience}`)} color="var(--chart-2)" />
                    )}
                  </div>

                  {item.intro ? (
                    <p className="text-muted-foreground mt-1 text-sm">{item.intro}</p>
                  ) : null}

                  <p className="text-muted-foreground mt-2 text-xs">
                    {item.publishedAt
                      ? format.dateTime(new Date(item.publishedAt), 'short')
                      : t('undated')}
                    {' · '}
                    {item.authorName}
                  </p>
                </div>

                <div className="flex shrink-0 gap-1">
                  <Button asChild variant="ghost" size="sm" aria-label={tCommon('view')}>
                    <Link href={`/news/${encodeURIComponent(item.id)}`}>
                      <Eye className="size-4" aria-hidden />
                    </Link>
                  </Button>

                  {canWrite ? (
                    <>
                      <Button asChild variant="ghost" size="sm" aria-label={tCommon('edit')}>
                        <Link href={`/news/${encodeURIComponent(item.id)}/edit`}>
                          <FileEdit className="size-4" aria-hidden />
                        </Link>
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={pending}
                        aria-label={tCommon('delete')}
                        onClick={() => remove(item.id, item.title)}
                      >
                        <Trash2 className="size-4" aria-hidden />
                      </Button>
                    </>
                  ) : null}
                </div>
              </CardContent>
            </Card>
          </li>
        ))}
      </ul>
    </div>
  );
}
