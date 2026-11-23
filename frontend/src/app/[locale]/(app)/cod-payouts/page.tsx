import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { ReceiptText } from 'lucide-react';
import { PageHeader } from '@/components/layout/page-header';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { CodPayoutsScreen } from '@/features/finance/cod-payouts-screen';
import { Link } from '@/i18n/navigation';
import { getCurrentUser } from '@/lib/api/auth';
import { getAgencies, getClients } from '@/lib/api/directory';
import { getCodPayoutPage } from '@/lib/api/finance';
import { getMerchantCompany } from '@/lib/api/merchant';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('finance.codPayouts');
  return { title: t('title') };
}

export default async function CodPayoutsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('finance.codPayouts');
  const user = await getCurrentUser();

  /*
   * « MES VERSEMENTS » D'UN CLIENT NORMAL — lot V, V4 (D-234). Il est réglé par
   * sa facture (D-061) : l'API lui rend une liste vide, et la page lui dit
   * pourquoi plutôt que d'afficher une table muette. Une lecture ratée du
   * régime laisse la page d'avant.
   */
  if (user?.clientId) {
    const merchant = await getMerchantCompany().catch(() => null);

    if (merchant?.billingType === 'normal') {
      return (
        <>
          <PageHeader title={t('title')} />
          <Card>
            <CardContent className="flex flex-col items-center gap-3 py-12 text-center">
              <ReceiptText className="text-muted-foreground size-8" />
              <p className="font-medium">{t('merchantNormal.title')}</p>
              <p className="text-muted-foreground max-w-md text-sm">{t('merchantNormal.body')}</p>
              <Button asChild variant="outline" size="sm">
                <Link href="/invoices">{t('merchantNormal.link')}</Link>
              </Button>
            </CardContent>
          </Card>
        </>
      );
    }
  }

  const [page, clients, agencies] = await Promise.all([
    getCodPayoutPage({
      ...listQueryFrom(params, { sort: 'createdAt', order: 'desc' }),
      status: firstParam(params.status),
      clientId: firstParam(params.clientId),
      agencyId: firstParam(params.agencyId),
    }),
    getClients(),
    getAgencies(),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <CodPayoutsScreen
        page={page}
        /*
         * Le filtre et le formulaire ne proposent que des PROFESSIONNELS : un
         * client normal est réglé par sa facture, et l'API refuserait de toute
         * façon d'ouvrir un bon pour lui (D-061). Ne pas le proposer évite
         * d'avoir à expliquer le refus.
         */
        clients={clients.filter((client) => client.billingType === 'professional')}
        agencies={agencies}
        canWrite={user?.permissions.includes('finance.write') ?? false}
        // Lot U (D-233) : la génération en masse est réservée à l'administration.
        canBulk={user?.permissions.includes('finance.validate') ?? false}
        merchant={Boolean(user?.clientId)}
      />
    </>
  );
}
