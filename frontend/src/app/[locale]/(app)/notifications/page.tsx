import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { NotificationFeedList } from '@/features/notifications/notification-feed';
import { getNotificationFeed } from '@/lib/api/notifications-feed';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('notificationCenter');
  return { title: t('title') };
}

/**
 * LE CENTRE DE NOTIFICATIONS — V2 vendeur bloc 8, spécification §10.
 *
 * **Ouvert à tout compte connecté**, et non au seul vendeur : un agent
 * d'agence reçoit lui aussi des notifications — écart de caisse, réclamation
 * en retard — et n'avait pas davantage d'endroit où les relire. La route
 * n'exige aucune permission, pour la même raison que les préférences : ses
 * propres notifications ne sont pas une ressource du domaine.
 */
export default async function NotificationsPage() {
  const [feed, t] = await Promise.all([
    getNotificationFeed(),
    getTranslations('notificationCenter'),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <NotificationFeedList items={feed.items} unread={feed.unread} />
    </>
  );
}
