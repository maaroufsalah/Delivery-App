import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { ProvidersScreen } from '@/features/settings/providers-screen';
import { getCurrentUser } from '@/lib/api/auth';
import { getCouriers } from '@/lib/api/directory';
import { getProviderCalls, getProviders } from '@/lib/api/providers';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('settings.providers');
  return { title: t('title') };
}

/**
 * Paramètres > Prestataires — chantier sync-wimo-speedy.
 *
 * Un prestataire (Wimo) est un livreur de la société dont le jeton pousse les
 * statuts. Les derniers appels de chaque jeton sont lus ici, au serveur : il y
 * en a un ou deux, pas de quoi paginer.
 */
export default async function ProviderSettingsPage() {
  const user = await getCurrentUser();
  // `notFound()` plutôt qu'un 403 : sans la clé, l'écran n'existe pas (comme le journal).
  if (!user?.permissions.includes('settings.read')) notFound();

  const [providers, couriers] = await Promise.all([getProviders(), getCouriers()]);
  const calls = Object.fromEntries(
    await Promise.all(
      providers.map(async (provider) => [provider.id, await getProviderCalls(provider.id)] as const),
    ),
  );

  return (
    <ProvidersScreen
      providers={providers}
      couriers={couriers.filter((courier) => courier.isActive)}
      calls={calls}
      canWrite={user.permissions.includes('settings.write')}
    />
  );
}
