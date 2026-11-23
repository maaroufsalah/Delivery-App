import type { Metadata } from 'next';
import { getTimeZone, getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { InvoicesScreen } from '@/features/finance/invoices-screen';
import { getCurrentUser } from '@/lib/api/auth';
import { getAgencies, getClients } from '@/lib/api/directory';
import { getInvoiceDays, getProInvoicePage } from '@/lib/api/finance';
import { invoiceDaysOf } from '@/lib/invoice-day';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('finance.invoices');
  return { title: t('titlePro') };
}

/**
 * « FACTURES CLIENTS PRO » — recette SpeedyTrans 3, point 4.
 *
 * La page des Clients Pro (`isPro`, le badge « Pro » de /clients). Elle lit
 * `/invoices-pro` : sa liste, son export, ses candidats de génération en masse
 * et d'ordre de virement, sa facture unitaire et sa facture du mois ne
 * concernent que ces clients-là. Le rendu est celui des factures clients —
 * ce sont les mêmes factures, calculées et imprimées de la même façon. Le
 * détail d'une facture reste commun (`/invoices/:code`).
 *
 * Back-office seulement : absente des routes vendeur, fermée côté API.
 */
export default async function ProInvoicesPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('finance.invoices');
  const filters = {
    status: firstParam(params.status),
    clientId: firstParam(params.clientId),
    agencyId: firstParam(params.agencyId),
  };
  const list = listQueryFrom(params, { sort: 'issuedAt', order: 'desc' });

  const [page, clients, agencies, user, timeZone] = await Promise.all([
    // D-254 : toujours rangée par jour d'émission, brouillons en tête.
    getProInvoicePage({ ...list, sort: 'issuedAt', ...filters }),
    getClients(),
    getAgencies(),
    getCurrentUser(),
    getTimeZone(),
  ]);

  // Les en-têtes des jours de CETTE page, aux mêmes filtres.
  const days =
    page.items.length > 0
      ? await getInvoiceDays('pro', {
          ...filters,
          search: list.search,
          days: invoiceDaysOf(page.items, timeZone).join(','),
        })
      : [];

  return (
    <>
      <PageHeader title={t('titlePro')} subtitle={t('subtitlePro')} />
      <InvoicesScreen
        kind="pro"
        page={page}
        days={days}
        clients={clients.filter((client) => client.isPro)}
        agencies={agencies}
        canWrite={user?.permissions.includes('finance.write') ?? false}
      />
    </>
  );
}
