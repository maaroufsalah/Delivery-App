import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { RemittanceDashboard } from '@/features/finance/remittance-dashboard';
import { RemittancesScreen } from '@/features/finance/remittances-screen';
import { getCurrentUser } from '@/lib/api/auth';
import { getAgencies, getCities, getCouriers } from '@/lib/api/directory';
import { getRemittancePage, getRemittanceSummary } from '@/lib/api/finance';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';
import { effectivePeriod } from '@/lib/dashboard-period';
import { readPeriodPreference } from '@/lib/dashboard-period.server';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('finance.remittances');
  return { title: t('title') };
}

export default async function RemittancesPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('finance.remittances');

  /*
   * LA PÉRIODE EFFECTIVE — chantier « Filtre de période », réutilisé tel quel.
   * URL, sinon cookie, sinon « ce mois ». Résolue AVANT l'appel pour que le
   * premier rendu agrège déjà la bonne fenêtre.
   *
   * Elle ne borne QUE les cartes. La liste garde ses propres filtres : la
   * borner au mois aurait fait disparaître les bons d'août d'un écran où ils
   * étaient visibles hier — une régression déguisée en fonctionnalité.
   */
  const remembered = await readPeriodPreference();
  const period = effectivePeriod(firstParam(params.period), remembered.period);
  const customFrom = firstParam(params.from) ?? remembered.range?.from;
  const customTo = firstParam(params.to) ?? remembered.range?.to;

  /*
   * LE STATUT AFFICHÉ — D-243. `stage` est le filtre de l'écran ; `status`
   * n'est plus transmis que pour les liens d'avant ce chantier
   * (`?status=confirmed`), et cède dès qu'un `stage` est choisi.
   *
   * « Caisse fermée » se lit de la plus ancienne à la plus récente : c'est la
   * file de ce que l'agence doit réceptionner. Le même défaut est posé dans
   * `RemittancesScreen`, pour que l'en-tête de colonne dise l'ordre servi.
   */
  const stage = firstParam(params.stage);

  const [page, summary, couriers, cities, agencies, user] = await Promise.all([
    getRemittancePage({
      ...listQueryFrom(params, {
        sort: 'createdAt',
        order: stage === 'closed' ? 'asc' : 'desc',
      }),
      stage,
      status: stage ? undefined : firstParam(params.status),
      courierId: firstParam(params.courierId),
      cityId: firstParam(params.cityId),
      agencyId: firstParam(params.agencyId),
    }),
    getRemittanceSummary({
      period,
      from: period === 'custom' ? customFrom : undefined,
      to: period === 'custom' ? customTo : undefined,
      courierId: firstParam(params.courierId),
      agencyId: firstParam(params.agencyId),
    }),
    getCouriers(),
    getCities(),
    getAgencies(),
    getCurrentUser(),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <div className="flex flex-col gap-6">
        <RemittanceDashboard summary={summary} />
        <RemittancesScreen
          page={page}
          couriers={couriers}
          cities={cities}
          agencies={agencies}
          canWrite={user?.permissions.includes('finance.write') ?? false}
        />
      </div>
    </>
  );
}
