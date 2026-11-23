import type { Metadata } from 'next';
import { getFormatter, getTranslations } from 'next-intl/server';
import { Card, CardContent } from '@/components/ui/card';
import { getAnnouncementFeed } from '@/lib/api/announcements';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courier.nav');
  return { title: t('news') };
}

/**
 * LES ACTUALITÉS DU LIVREUR — L1 bloc 1.
 *
 * `GET /announcements/feed` ne porte AUCUNE permission : il rend « les
 * actualités qui concernent ce compte », bornées par la portée serveur. C'est
 * le seul des trois libellés que le menu mobile n'a pas pu servir faute
 * d'endpoint ouvert — ici il l'est, et l'écran est le même pour tous les rôles.
 */
export default async function CourierNewsPage() {
  const [t, tNav, format, news] = await Promise.all([
    getTranslations('courier.home'),
    getTranslations('courier.nav'),
    getFormatter(),
    getAnnouncementFeed(20).catch(() => []),
  ]);

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold tracking-tight">{tNav('news')}</h1>

      {news.length === 0 ? (
        <Card>
          <CardContent className="text-muted-foreground p-8 text-center text-sm">
            {t('noNews')}
          </CardContent>
        </Card>
      ) : (
        <ul className="flex flex-col gap-3">
          {news.map((item) => (
            <li key={item.id}>
              <Card>
                <CardContent className="flex flex-col gap-1.5 p-4">
                  <span className="text-sm font-medium">{item.title}</span>
                  {item.publishedAt ? (
                    <span className="text-muted-foreground text-xs">
                      {format.dateTime(new Date(item.publishedAt), 'stamp')}
                    </span>
                  ) : null}
                  {item.intro ? <p className="text-sm">{item.intro}</p> : null}
                </CardContent>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
