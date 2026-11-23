import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { PageHeader } from '@/components/layout/page-header';
import { InvoiceDetailScreen } from '@/features/finance/invoice-detail';
import { getCurrentUser } from '@/lib/api/auth';
import { getBankAccounts, getBillableParcels, getInvoice } from '@/lib/api/finance';
import { ApiError } from '@/lib/http';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ code: string }>;
}): Promise<Metadata> {
  const { code } = await params;
  return { title: decodeURIComponent(code) };
}

export default async function InvoiceDetailPage({ params }: { params: Promise<{ code: string }> }) {
  const { code } = await params;
  const t = await getTranslations('finance.invoices');

  const invoice = await getInvoice(decodeURIComponent(code)).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  const user = await getCurrentUser();
  const canWrite = user?.permissions.includes('finance.write') ?? false;

  /*
   * La pile des facturables n'est chargée que pour un BROUILLON qu'on a le
   * droit de composer. Sur une facture émise elle ne sert à rien, et
   * l'e-commerçant n'a pas à connaître les colis que l'agence n'a pas encore
   * facturés — l'API le lui refuserait de toute façon.
   */
  const [billable, accounts] = await Promise.all([
    canWrite && invoice.status === 'draft'
      ? getBillableParcels({ clientId: invoice.clientId, agencyId: invoice.agencyId })
      : Promise.resolve([]),
    user?.permissions.includes('finance.pay') ? getBankAccounts() : Promise.resolve([]),
  ]);

  return (
    <>
      {/* Retour au sous-menu de la facture — recette 3, point 4. */}
      <PageHeader
        backHref={invoice.clientIsPro && !user?.clientId ? '/invoices-pro' : '/invoices'}
        title={invoice.code}
        subtitle={t('subtitle')}
      />
      <InvoiceDetailScreen
        invoice={invoice}
        billable={billable}
        accounts={accounts}
        canWrite={canWrite}
        canValidate={user?.permissions.includes('finance.validate') ?? false}
        canPay={user?.permissions.includes('finance.pay') ?? false}
        internalView={!user?.clientId}
      />
    </>
  );
}
