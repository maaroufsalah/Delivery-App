import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { NewsScreen } from '@/features/referential/news-screen';
import { getAnnouncementFeed, getAnnouncements } from '@/lib/api/announcements';
import { getCurrentUser } from '@/lib/api/auth';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('referential.news');
  return { title: t('title') };
}

/**
 * Actualités — DOMAIN.md §1.20.
 *
 * Deux lectures pour une seule route : le staff obtient la vue de RÉDACTION,
 * brouillons compris ; tout autre compte obtient ce qui le CONCERNE, déjà
 * filtré par l'API sur sa cible. Deux routes auraient dupliqué l'écran pour
 * une différence qui tient dans une requête.
 */
export default async function NewsPage() {
  const user = await getCurrentUser();
  const canWrite = user?.permissions.includes('announcements.write') ?? false;

  const [items, t] = await Promise.all([
    canWrite
      ? getAnnouncements({ page: 1, pageSize: 50 }).then((page) => page.items)
      : getAnnouncementFeed(50),
    getTranslations('referential.news'),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <NewsScreen items={items} canWrite={canWrite} />
    </>
  );
}
