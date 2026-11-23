import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { Backpack } from 'lucide-react';
import { PageHeader } from '@/components/layout/page-header';
import { Button } from '@/components/ui/button';
import { CourierTabs } from '@/features/accounts/courier-tabs';
import { getCourier, getUser } from '@/lib/api/accounts';
import { getCurrentUser } from '@/lib/api/auth';
import { getAgencies, getAllCities, getZones } from '@/lib/api/directory';
import { getBankAccounts, getCourierStatementPage, getCourierBalance } from '@/lib/api/finance';
import { Link } from '@/i18n/navigation';
import { ApiError } from '@/lib/http';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ id: string }>;
}): Promise<Metadata> {
  const { id } = await params;

  try {
    const courier = await getCourier(id);
    return { title: `${courier.firstName} ${courier.lastName}` };
  } catch {
    const t = await getTranslations('accounts.couriers');
    return { title: t('title') };
  }
}

/**
 * Fiche livreur.
 *
 * Villes désactivées comprises : une grille de paie peut porter une ville
 * retirée du référentiel, et ne pas savoir la nommer afficherait un
 * identifiant à la place d'un nom.
 *
 * Les relevés de rémunération (D-020) ne sont chargés que pour un compte qui a
 * le droit de les lire : sans `finance.read`, l'onglet n'existe pas et l'appel
 * ne part pas.
 */
export default async function CourierDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  const courier = await getCourier(id).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  const [cities, agencies, user, account, zones] = await Promise.all([
    getAllCities(),
    getAgencies(),
    getCurrentUser(),
    /*
     * Le compte lié, chargé ICI — C13 bloc 3. L'onglet « Compte » est un
     * composant client : il ne peut pas aller le chercher lui-même (D-018).
     * Best-effort : un compte supprimé entre-temps ne doit pas rendre la fiche
     * livreur inaccessible.
     */
    courier.userId ? getUser(courier.userId).catch(() => null) : Promise.resolve(null),
    // Lot F (D-206) : le filtre Zone du tableau des villes.
    getZones(),
  ]);

  const showStatements = user?.permissions.includes('finance.read') ?? false;
  const tStock = await getTranslations('courierStock');

  const [statements, balance, accounts] = await Promise.all([
    showStatements
      ? getCourierStatementPage({ courierId: id, page: 1, pageSize: 24, order: 'desc' })
      : Promise.resolve(null),
    /*
     * LE SOLDE — chantier « Finance », point 4. Même garde que les relevés :
     * c'est de la trésorerie, et qui ne la lit pas n'en voit pas l'onglet.
     * Best-effort — une lecture qui échoue ne doit pas rendre la fiche du
     * livreur inaccessible.
     */
    showStatements ? getCourierBalance(id).catch(() => null) : Promise.resolve(null),
    // Les comptes d'où peut partir la paie — C11 bloc 7. Chargés seulement
    // pour qui peut payer : les autres n'ouvriront jamais le formulaire.
    user?.permissions.includes('finance.pay') ? getBankAccounts() : Promise.resolve([]),
  ]);

  return (
    <>
      <PageHeader
        backHref="/couriers"
        title={`${courier.firstName} ${courier.lastName}`}
        subtitle={courier.phone}
        actions={
          // Lot M (D-222) : le stock de ce livreur, même écran que « Stock Livreur ».
          user?.permissions.includes('routes.stock') ? (
            <Button asChild variant="outline" size="sm" className="gap-1.5">
              <Link href={`/courier-stock/${courier.id}`}>
                <Backpack className="size-4" aria-hidden />
                {tStock('courierLink')}
              </Link>
            </Button>
          ) : null
        }
      />
      <CourierTabs
        courier={courier}
        account={account}
        cities={cities}
        agencies={agencies}
        zones={zones}
        statements={statements?.items ?? []}
        balance={balance}
        accounts={accounts}
        showStatements={showStatements}
        canWrite={user?.permissions.includes('finance.write') ?? false}
        canValidate={user?.permissions.includes('finance.validate') ?? false}
        canPay={user?.permissions.includes('finance.pay') ?? false}
        canManageAccounts={user?.permissions.includes('accounts.write') ?? false}
      />
    </>
  );
}
