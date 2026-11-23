import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { PageHeader } from '@/components/layout/page-header';
import { EntityHistory } from '@/features/audit/entity-history';
import { AccountPanel } from '@/features/accounts/account-panel';
import { UserPermissionsButton } from '@/features/accounts/user-permissions-dialog';
import { getAgencyPage, getUser, getUserPermissions } from '@/lib/api/accounts';
import { getCurrentUser } from '@/lib/api/auth';
import { ApiError } from '@/lib/http';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ id: string }>;
}): Promise<Metadata> {
  const { id } = await params;

  try {
    const user = await getUser(id);
    return { title: `${user.firstName} ${user.lastName}` };
  } catch {
    const t = await getTranslations('accounts.users');
    return { title: t('title') };
  }
}

/**
 * La fiche d'un COMPTE — C13 bloc 3.
 *
 * C'est aussi la fiche d'un membre de « Mon équipe » : la mission en demandait
 * une, et il n'y en avait aucune. En faire deux pages distinctes aurait donné
 * deux formulaires pour le même objet, selon la porte d'entrée — exactement ce
 * que ce bloc supprime.
 *
 * L'écran est le MÊME que l'onglet « Compte » d'une fiche client ou livreur :
 * un `AccountPanel`, et rien d'autre.
 */
export default async function UserDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  const user = await getUser(id).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  const [agencies, current, permissions] = await Promise.all([
    getAgencyPage({ page: 1, pageSize: 200, sort: 'name', order: 'asc' }),
    getCurrentUser(),
    getUserPermissions(id),
  ]);

  const canManage = current?.permissions.includes('accounts.write') ?? false;

  return (
    <>
      <PageHeader
        backHref="/users"
        title={`${user.firstName} ${user.lastName}`}
        subtitle={user.email ?? user.username ?? undefined}
        actions={<UserPermissionsButton initial={permissions} canManage={canManage} />}
      />

      <AccountPanel user={user} agencies={agencies.items} canManage={canManage} />

      {/* SOUS le panneau : l'historique porte sur le compte entier. */}
      <EntityHistory entity="User" entityId={user.id} />
    </>
  );
}
