import type { Metadata } from 'next';
import { getTimeZone, getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { InvoicesScreen } from '@/features/finance/invoices-screen';
import { getCurrentUser } from '@/lib/api/auth';
import { getAgencies, getClients } from '@/lib/api/directory';
import { getInvoiceDays, getInvoicePage } from '@/lib/api/finance';
import { invoiceDaysOf } from '@/lib/invoice-day';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('finance.invoices');
  return { title: t('title') };
}

export default async function InvoicesPage({
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
    getInvoicePage({ ...list, sort: 'issuedAt', ...filters }),
    getClients(),
    getAgencies(),
    getCurrentUser(),
    getTimeZone(),
  ]);

  // Les en-têtes des jours de CETTE page, aux mêmes filtres.
  const days =
    page.items.length > 0
      ? await getInvoiceDays('normal', {
          ...filters,
          search: list.search,
          days: invoiceDaysOf(page.items, timeZone).join(','),
        })
      : [];

  /*
   * « FACTURES CLIENTS » — recette SpeedyTrans 3, point 4. Au back-office, la
   * page ne concerne que les clients normaux (`isPro` faux) : filtres, facture
   * unitaire et facture du mois ne proposent qu'eux, l'API refuse les autres.
   * Le VENDEUR, lui, garde « Factures » et toutes les siennes.
   */
  const merchant = Boolean(user?.clientId);
  const pageClients = merchant ? clients : clients.filter((client) => !client.isPro);

  return (
    <>
      <PageHeader
        title={merchant ? t('title') : t('titleClients')}
        subtitle={merchant ? t('subtitleMerchant') : t('subtitle')}
      />
      <InvoicesScreen
        kind="normal"
        page={page}
        days={days}
        clients={pageClients}
        agencies={agencies}
        // Lot V, V2 : ni colonne ni filtre « E-commerçant » chez le vendeur.
        showClient={!merchant}
        // L'e-commerçant n'a que `finance.read` : la portée serveur lui montre
        // SES factures, et l'écran ne lui propose aucun geste d'écriture.
        canWrite={user?.permissions.includes('finance.write') ?? false}
      />
    </>
  );
}
