import type { Metadata } from 'next';
import { getFormatter, getTranslations } from 'next-intl/server';
import { Card, CardContent } from '@/components/ui/card';
import { getAnnouncementFeed } from '@/lib/api/announcements';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('merchantNav');
  return { title: t('news') };
}

/**
 * LE FLUX D'ACTUALITÉS — L1 bloc 7.
 *
 * **Le seul écran neuf de la coque e-commerçant.** Les actualités existaient
 * en deux endroits : l'éditeur `/news`, réservé à qui porte
 * `announcements.write`, et le bandeau du tableau de bord, qui n'en montre que
 * trois. Un vendeur qui voulait relire une annonce de la semaine dernière
 * n'avait nulle part où aller.
 *
 * `GET /announcements/feed` ne porte AUCUNE permission : il rend « les
 * actualités qui concernent ce compte », bornées par la portée serveur. Le
 * même endpoint sert l'accueil du livreur et celui du mobile.
 */
export default async function FeedPage() {
  const [t, tNav, format, news] = await Promise.all([
    getTranslations('courier.home'),
    getTranslations('merchantNav'),
    getFormatter(),
    getAnnouncementFeed(20).catch(() => []),
  ]);

  return (
    <div className="flex max-w-3xl flex-col gap-4">
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
