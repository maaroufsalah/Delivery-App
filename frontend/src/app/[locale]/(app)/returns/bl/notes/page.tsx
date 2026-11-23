import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { BlNotesScreen } from '@/features/returns/bl-notes-screen';
import { getBlNotePage } from '@/lib/api/bl';
import { getClients, getCouriers } from '@/lib/api/directory';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

/**
 * ADMIN → RETOURS → ONGLET BL — chantier « Retours, BL Clients Pro », point 5.
 *
 * L'annuaire complet est chargé, et l'écran n'en garde que les Clients Pro
 * pour son filtre : c'est la même lecture que partout, et un endpoint dédié
 * « clients pro » aurait fait un second endroit où la règle se décide.
 */
export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('returns.bl');
  return { title: t('title') };
}

export default async function BlNotesPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('returns.bl');

  const [page, clients, couriers] = await Promise.all([
    getBlNotePage({
      ...listQueryFrom(params, { sort: 'createdAt', order: 'desc' }),
      status: firstParam(params.status),
      clientId: firstParam(params.clientId),
      courierId: firstParam(params.courierId),
    }),
    getClients(),
    getCouriers(),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <BlNotesScreen page={page} clients={clients} couriers={couriers} />
    </>
  );
}
