import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { PageHeader } from '@/components/layout/page-header';
import { ClientTabs } from '@/features/accounts/client-tabs';
import { getClient, getUser } from '@/lib/api/accounts';
import { getCurrentUser } from '@/lib/api/auth';
import { getAgencies, getCities, getDistricts, getZones } from '@/lib/api/directory';
import { quoteParcel } from '@/lib/api/parcels';
import { getFinanceSettings } from '@/lib/api/settings';
import { ApiError } from '@/lib/http';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ id: string }>;
}): Promise<Metadata> {
  const { id } = await params;

  try {
    const client = await getClient(id);
    return { title: client.name };
  } catch {
    const t = await getTranslations('accounts.clients');
    return { title: t('title') };
  }
}

/**
 * Fiche e-commerçant.
 *
 * Villes, quartiers et zones descendent en entier avec la page : les
 * sous-formulaires en ont besoin, et un composant client ne peut pas les
 * demander lui-même (D-018).
 */
export default async function ClientDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  const client = await getClient(id).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  const [cities, districts, zones, agencies, user, account, sameCity, finance] = await Promise.all([
    getCities(),
    getDistricts(),
    getZones(),
    getAgencies(),
    getCurrentUser(),
    /*
     * Le compte lié, chargé ICI — C13 bloc 3 : l'onglet « Compte » est un
     * composant client et ne peut pas aller le chercher (D-018). Best-effort,
     * pour qu'un compte supprimé entre-temps ne rende pas la fiche
     * inaccessible.
     */
    client.userId ? getUser(client.userId).catch(() => null) : Promise.resolve(null),
    /*
     * Le TARIF SOCIÉTÉ du client, onglet « Tarifs & remises » (lot K) : le devis
     * lui-même (`quoteDeliveryFee`), sans `clientId` donc sans remise, pour un
     * colis ramassé et livré dans la ville de la fiche — la ville de départ
     * par défaut d'un colis. Best-effort : sans lui, la fiche s'affiche.
     */
    quoteParcel({
      originCityId: client.cityId,
      destinationCityId: client.cityId,
      deliveryTarget: 'district',
    }).catch(() => null),
    /*
     * Lot R (D-230) : le défaut société de la TVA pro, pour que « défaut »
     * dise son chiffre ; lot T (D-232) : le ramassage global, pour TOUT
     * client. Best-effort : sans lui, les champs restent sans chiffre.
     */
    getFinanceSettings().catch(() => null),
  ]);

  const originCity = cities.find((city) => city.id === client.cityId);

  return (
    <>
      <PageHeader
        backHref="/clients"
        title={client.name}
        subtitle={client.legalName ?? client.email}
      />
      <ClientTabs
        client={client}
        account={account}
        cities={cities}
        districts={districts}
        zones={zones}
        agencies={agencies}
        companyRate={{
          originCityName: originCity?.name ?? client.cityName,
          originZoneName: zones.find((zone) => zone.id === originCity?.zoneId)?.name ?? null,
          sameCityFee: sameCity?.fee ?? null,
        }}
        canManageAccounts={user?.permissions.includes('accounts.write') ?? false}
        proVat={{
          canEdit: user?.permissions.includes('finance.validate') ?? false,
          // Paramètres > Finance rend des POURCENTS (B-14) ; la fiche parle en centièmes.
          companyDefault: finance ? Math.round(finance.proVatRate * 100) : null,
        }}
        // Paramètres > Finance rend des DIRHAMS ; la fiche parle en centimes.
        pickupDefaults={
          finance
            ? {
                fee: Math.round(finance.pickupFee * 100),
                freeFrom: finance.freePickupThreshold,
              }
            : null
        }
      />
    </>
  );
}
