import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { NotesTable } from '@/features/notes/notes-table';
import { getCurrentUser } from '@/lib/api/auth';
import { getAgencies, getCities, getClients } from '@/lib/api/directory';
import { getDeliveryNotes, getDeliveryNoteStats } from '@/lib/api/routes';
import { firstParam as first, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('nav.items');
  return { title: t('pickupNotes') };
}

export default async function PickupNotesPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;

  // La page demandée, et les compteurs de la sélection entière (B-5, C7 bloc 4).
  const filters = {
    kind: 'pickup' as const,
    search: first(params.search),
    status: first(params.status),
    clientId: first(params.clientId),
  };

  const [page, stats, clients, agencies, cities, user, t, tNav] = await Promise.all([
    getDeliveryNotes({
      ...listQueryFrom(params, { sort: 'createdAt', order: 'desc' }),
      ...filters,
    }),
    getDeliveryNoteStats(filters),
    getClients(),
    getAgencies(),
    getCities(),
    getCurrentUser(),
    getTranslations('notes'),
    getTranslations('nav.items'),
  ]);

  /*
    LE VENDEUR DEMANDE, IL NE COMPOSE PAS — retouches Anass, points 2 et 3.

    Le formulaire de l'administration poste sur `POST /delivery-notes`, gardé
    par `notes.write` : un vendeur y recevait « Permission insuffisante ». La
    liste de SES colis en attente est donc chargée ici, et le bouton ouvre le
    choisisseur plutôt que le formulaire.
  */
  const selfClientId = user?.clientId ?? null;

  return (
    <>
      <PageHeader title={tNav('pickupNotes')} subtitle={t('subtitle.pickup')} />
      <NotesTable
        page={page}
        stats={stats}
        clients={clients}
        agencies={agencies}
        cities={cities}
        kind="pickup"
        selfClientId={selfClientId}
      />
    </>
  );
}
