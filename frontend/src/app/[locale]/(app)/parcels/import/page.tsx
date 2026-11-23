import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { ParcelImportHistory } from '@/features/parcels/import/import-history';
import { ParcelImportScreen } from '@/features/parcels/import/import-screen';
import { getCurrentUser } from '@/lib/api/auth';
import { getClients } from '@/lib/api/directory';
import { getParcelImports } from '@/lib/api/parcels';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('parcels.import');
  return { title: t('title') };
}

/**
 * Import Excel des colis — C7 bloc 1.
 *
 * Un écran plutôt qu'un dialogue : la relecture ligne par ligne d'un fichier de
 * plusieurs centaines de lignes, avec correction des cellules en erreur, ne
 * tient pas dans une fenêtre modale. Le bouton « Importer » de la liste des
 * colis y conduit.
 *
 * L'e-commerçant importe pour lui-même : le sélecteur disparaît de son écran,
 * et la portée serveur l'y borne de toute façon.
 */
export default async function ParcelImportPage() {
  const [clients, user, history] = await Promise.all([
    getClients(),
    getCurrentUser(),
    getParcelImports({ page: 1, pageSize: 20 }),
  ]);

  const t = await getTranslations('parcels.import');
  const merchant = user?.roles.includes('MERCHANT') ?? false;

  return (
    <>
      <PageHeader backHref="/parcels" title={t('title')} subtitle={t('subtitle')} />

      <ParcelImportScreen
        clients={clients}
        lockedClientId={merchant ? (clients[0]?.id ?? undefined) : undefined}
      />

      <section className="mt-8 flex flex-col gap-3">
        <h2 className="text-base font-semibold">{t('history.title')}</h2>
        <ParcelImportHistory items={history.items} />
      </section>
    </>
  );
}
