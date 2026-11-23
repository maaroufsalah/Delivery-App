import type { AnnouncementDetail } from '@delivery/shared';
import { Megaphone } from 'lucide-react';
import { getFormatter, getTranslations } from 'next-intl/server';
import { Card, CardContent } from '@/components/ui/card';
import { Link } from '@/i18n/navigation';

/**
 * Bandeau « Nouveautés » du tableau de bord — C7 bloc 3.
 *
 * Les TROIS dernières actualités qui concernent le compte, et rien de plus :
 * un bandeau qui grossit cesse d'être lu. L'API a déjà filtré sur la cible —
 * un livreur n'y voit pas les annonces adressées aux e-commerçants.
 *
 * Composant SERVEUR : il n'a aucun état, et le rendre client ferait voyager
 * trois textes déjà présents dans le HTML.
 */
export async function NewsBanner({ items }: { items: AnnouncementDetail[] }) {
  if (items.length === 0) return null;

  const [t, format] = await Promise.all([getTranslations('referential.news'), getFormatter()]);

  return (
    <Card className="border-primary/30 bg-primary/5">
      <CardContent className="flex flex-col gap-3 pt-5">
        <div className="flex items-center justify-between gap-3">
          <h2 className="text-primary inline-flex items-center gap-2 text-sm font-semibold">
            <Megaphone className="size-4" aria-hidden />
            {t('banner')}
          </h2>
          <Link href="/news" className="text-muted-foreground text-xs hover:underline">
            {t('seeAll')}
          </Link>
        </div>

        <ul className="flex flex-col gap-2">
          {items.map((item) => (
            <li key={item.id} className="flex flex-col gap-0.5">
              <Link
                href={`/news/${encodeURIComponent(item.id)}`}
                className="text-sm font-medium hover:underline"
              >
                {item.title}
              </Link>
              {item.intro ? (
                <p className="text-muted-foreground line-clamp-2 text-xs">{item.intro}</p>
              ) : null}
              {item.publishedAt ? (
                <p className="text-muted-foreground text-[11px]">
                  {format.dateTime(new Date(item.publishedAt), 'short')}
                </p>
              ) : null}
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
