import type { ParcelFilters } from '@delivery/shared';
import { Plus } from 'lucide-react';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { Button } from '@/components/ui/button';
import { parcelListFilters } from '@/features/parcels/list-filters';
import { ParcelsTable } from '@/features/parcels/parcels-table';
import { Link } from '@/i18n/navigation';
import { getCities, getClients, getCouriers } from '@/lib/api/directory';
import { getCurrentUser } from '@/lib/api/auth';
import { getParcels, getParcelStats } from '@/lib/api/parcels';
import { effectivePeriod } from '@/lib/dashboard-period';
import { readPeriodPreference } from '@/lib/dashboard-period.server';
import { firstParam as first, listQueryFrom, type SearchParams } from '@/lib/list-query';

export default async function ParcelsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('parcels');

  /*
   * LA PÉRIODE — chantier « Retours recette 26/09 », point 1. Même logique que
   * les tableaux de bord (URL, puis mémoire, puis défaut), avec SA mémoire et
   * SON défaut : « depuis le début », pour qu'une liste ne masque jamais un
   * colis sans qu'on le lui ait demandé.
   */
  const remembered = await readPeriodPreference('parcels');
  const period = effectivePeriod(first(params.period), remembered.period, 'all');
  const customFrom = first(params.from) ?? remembered.range?.from;
  const customTo = first(params.to) ?? remembered.range?.to;

  const filters: ParcelFilters = {
    // Lot D, D4 : les mêmes clés que les étiquettes de « toute la liste ».
    ...parcelListFilters((key) => first(params[key])),
    period,
    from: period === 'custom' ? customFrom : undefined,
    to: period === 'custom' ? customTo : undefined,
  };

  // La page demandée, et elle seule : le découpage est fait par l'API (B-5).
  const [page, stats, clients, cities, user] = await Promise.all([
    // Les plus récents en premier (chantier « Tri par défaut », 11/11/2026).
    // prettier-ignore — une ligne : `parcel-list-filters.spec.ts` lit cette forme.
    getParcels({ ...listQueryFrom(params, { sort: 'createdAt', order: 'desc' }), recent: true, ...filters }),
    getParcelStats(filters),
    getClients(),
    getCities(),
    getCurrentUser(),
  ]);
  // D-246 : « En cours de livraison » posé depuis la liste demande un livreur.
  // « Transférer à un livreur » (cochés) lit la même liste, pour qui compose les circuits.
  const canTransfer = Boolean(user && !user.clientId && user.permissions.includes('routes.write'));
  const couriers =
    user && !user.clientId && (user.permissions.includes('parcels.status') || canTransfer)
      ? await getCouriers().catch(() => null)
      : null;

  return (
    <>
      <PageHeader
        title={t('title')}
        subtitle={t('subtitle')}
        actions={
          <Button asChild size="sm" className="gap-1.5">
            <Link href="/parcels/new">
              <Plus className="size-4" />
              <span className="hidden sm:inline">{t('new')}</span>
            </Link>
          </Button>
        }
      />
      <ParcelsTable
        page={page}
        stats={stats}
        clients={clients}
        cities={cities}
        /* La fenêtre vient de l'API : la page a demandé une période, l'API l'a résolue. */
        window={stats.window ?? { period, from: '', to: '', granularity: 'day' }}
        isMerchant={Boolean(user?.clientId)}
        statusCouriers={couriers}
        canTransfer={canTransfer}
        /* D-245 : le rôle, comme sur la fiche. L'API refuse tout autre compte en 403. */
        canAdminFieldStatus={!user?.clientId && (user?.roles.includes('SUPER_ADMIN') ?? false)}
      />
    </>
  );
}
