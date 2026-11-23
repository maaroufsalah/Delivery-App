import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { ParcelForm } from '@/features/parcels/parcel-form';
import { getAgencies, getCities, getClients, getDistricts } from '@/lib/api/directory';
import { getCurrentUser } from '@/lib/api/auth';
import { getMerchantCompany } from '@/lib/api/merchant';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('parcels.form');
  return { title: t('title') };
}

export default async function NewParcelPage() {
  const [clients, cities, agencies, districts, user] = await Promise.all([
    getClients(),
    getCities(),
    getAgencies(),
    getDistricts(),
    getCurrentUser(),
  ]);

  /*
    SA FICHE, pour préremplir « Expéditeur et ramassage » — point 9.

    Chargée SEULEMENT pour un vendeur : `GET /merchant/me/company` est gardée
    par `MerchantGuard`, et l'appeler pour un agent d'agence rendrait 403 sur
    un écran qui marche très bien sans.

    L'appel est best-effort : une fiche illisible doit coûter le
    préremplissage, pas l'écran de création de colis. Le formulaire retombe
    alors sur les champs ordinaires.
  */
  const sender = user?.clientId
    ? await getMerchantCompany()
        .then((company) => ({
          cityId: company.cityId,
          cityName: company.cityName,
          address: company.address,
        }))
        .catch(() => null)
    : null;

  const t = await getTranslations('parcels.form');

  return (
    <>
      <PageHeader backHref="/parcels" title={t('title')} subtitle={t('subtitle')} />
      {/*
        LE VENDEUR NE CHOISIT PAS SON PROPRE NOM — règle d'Anass.

        `clientId` du compte quand il y en a un : le champ disparaît et la
        valeur est fixée. L'administration, elle, garde le choix — c'est la
        seule différence entre les deux usages du même formulaire.
      */}
      <ParcelForm
        clients={clients}
        cities={cities}
        agencies={agencies}
        districts={districts}
        selfClientId={user?.clientId ?? null}
        sender={sender}
      />
    </>
  );
}
