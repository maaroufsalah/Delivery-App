import type { Metadata } from 'next';
import { ClipboardList } from 'lucide-react';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { Button } from '@/components/ui/button';
import { DeliverySlipsScreen } from '@/features/returns/delivery-slips-screen';
import { Link } from '@/i18n/navigation';
import { getCurrentUser } from '@/lib/api/auth';
import { getDeliverySlipPage } from '@/lib/api/bl';
import { getClients, getCouriers } from '@/lib/api/directory';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

/**
 * « BONS DE LIVRAISON » — lot E, E1.2 (D-201).
 *
 * La liste des BL, pour l'admin ET pour le vendeur (même route, même
 * permission `returns.read`) : la portée serveur ne rend au vendeur que les
 * siens. Les bons BRB- — les liasses que les livreurs rapportent — restent
 * consultables à `/returns/bl/notes`, et leur fiche ne bouge pas.
 */
export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('returns.deliverySlips');
  return { title: t('title') };
}

export default async function DeliverySlipsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('returns.deliverySlips');

  const [user, page, clients, couriers] = await Promise.all([
    getCurrentUser(),
    getDeliverySlipPage({
      ...listQueryFrom(params, { sort: 'collectedAt', order: 'desc' }),
      status: firstParam(params.status),
      clientId: firstParam(params.clientId),
      courierId: firstParam(params.courierId),
      from: firstParam(params.from),
      to: firstParam(params.to),
    }),
    getClients(),
    getCouriers(),
  ]);

  const merchant = Boolean(user?.clientId);

  return (
    <>
      <PageHeader
        title={t('title')}
        subtitle={merchant ? t('mineSubtitle') : t('subtitle')}
        actions={
          merchant ? null : (
            <Button asChild size="sm" variant="outline" className="gap-1.5">
              <Link href="/returns/bl/notes">
                <ClipboardList className="size-4" aria-hidden />
                {t('notesLink')}
              </Link>
            </Button>
          )
        }
      />
      <DeliverySlipsScreen
        page={page}
        clients={clients}
        couriers={couriers}
        merchant={merchant}
        canConfirm={!merchant && Boolean(user?.permissions.includes('returns.confirm'))}
        canRevert={Boolean(user?.roles.includes('SUPER_ADMIN'))}
      />
    </>
  );
}
