import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { FinanceDashboardScreen } from '@/features/finance/finance-dashboard';
import { FinanceFilters } from '@/features/finance/finance-filters';
import { FinanceOverviewScreen } from '@/features/finance/finance-overview';
import { getAgencies, getCouriers } from '@/lib/api/directory';
import { getFinanceDashboard, getFinanceOverview } from '@/lib/api/finance';
import { firstParam, type SearchParams } from '@/lib/list-query';
import { effectivePeriod } from '@/lib/dashboard-period';
import { readPeriodPreference } from '@/lib/dashboard-period.server';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('finance.dashboard');
  return { title: t('title') };
}

/**
 * LE TABLEAU DE BORD FINANCIER — chantier « Finance », point 10, D-112.
 *
 * Deux sections, deux questions : le POINT DU JOUR répond « qu'est-ce que je
 * fais maintenant » et pointe vers les écrans où agir ; le TABLEAU répond
 * « qu'est-ce qui s'est passé sur la période ». Les fondre aurait obligé à
 * choisir entre les deux.
 *
 * Une seule barre de filtres, en tête, et elle porte sur toute la page.
 */
export default async function FinanceDashboardPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const agencyId = firstParam(params.agencyId);

  /*
   * LA PÉRIODE EFFECTIVE — chantier « Filtre de période ». Le même sélecteur
   * que le tableau de bord principal, la même mémoire : URL, sinon cookie,
   * sinon « ce mois ». Résolue avant l'appel pour que le premier rendu agrège
   * la bonne fenêtre.
   */
  const remembered = await readPeriodPreference();
  const period = effectivePeriod(firstParam(params.period), remembered.period);
  const customFrom = firstParam(params.from) ?? remembered.range?.from;
  const customTo = firstParam(params.to) ?? remembered.range?.to;

  const [data, overview, agencies, couriers, t] = await Promise.all([
    getFinanceDashboard(agencyId),
    getFinanceOverview({
      agencyId,
      period,
      from: period === 'custom' ? customFrom : undefined,
      to: period === 'custom' ? customTo : undefined,
      courierId: firstParam(params.courierId),
    }),
    getAgencies(),
    getCouriers(),
    getTranslations('finance.dashboard'),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />

      <div className="flex flex-col gap-6">
        {/* Les bornes viennent du SERVEUR : sans filtre saisi, l'écran affiche
            le mois en cours parce que c'est ce que le serveur a lu, et non
            parce qu'il l'aurait recalculé de son côté. */}
        <FinanceFilters
          agencies={agencies}
          couriers={couriers}
          period={overview.period}
          from={overview.from}
          to={overview.to}
        />

        <FinanceDashboardScreen data={data} />
        <FinanceOverviewScreen data={overview} />
      </div>
    </>
  );
}
