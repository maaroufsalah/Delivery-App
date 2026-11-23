import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { RolesScreen } from '@/features/accounts/roles-screen';
import { getRoleMatrix } from '@/lib/api/accounts';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('accounts.roles');
  return { title: t('title') };
}

/**
 * Rôles et permissions. La matrice n'est pas paginée : c'est un quadrillage
 * de 4 rôles sur un catalogue fixe, et une « page 2 » n'aurait pas de sens.
 */
export default async function RolesPage() {
  const t = await getTranslations('accounts.roles');
  const matrix = await getRoleMatrix();

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <RolesScreen matrix={matrix} />
    </>
  );
}
