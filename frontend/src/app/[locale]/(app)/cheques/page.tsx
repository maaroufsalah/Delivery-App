import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { ChequesScreen } from '@/features/finance/cheques-screen';
import { getCurrentUser } from '@/lib/api/auth';
import { getClients, getCouriers } from '@/lib/api/directory';
import { getCheques } from '@/lib/api/finance';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('finance.cheques');
  return { title: t('title') };
}

/**
 * Les chèques reçus — chantier « Finance », point 9.
 *
 * La même page sert l'agence et le LIVREUR : la portée du serveur borne la
 * liste à ce qu'il a reçu, et le service ne lui laisse qu'un geste. Deux écrans
 * pour la même table auraient divergé au premier filtre ajouté.
 */
export default async function ChequesPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('finance.cheques');

  const [user, page, couriers, clients] = await Promise.all([
    getCurrentUser(),
    getCheques({
      ...listQueryFrom(params, { sort: 'receivedAt', order: 'desc' }),
      status: firstParam(params.status),
      courierId: firstParam(params.courierId),
      clientId: firstParam(params.clientId),
      bank: firstParam(params.bank),
      from: firstParam(params.from),
      to: firstParam(params.to),
      search: firstParam(params.search),
    }),
    getCouriers().catch(() => []),
    /* Le filtre vendeur — point 2. Best-effort : la portée d'un livreur ne
       donne pas l'annuaire, et le filtre disparaît alors de lui-même. */
    getClients().catch(() => []),
  ]);

  /*
   * « MES CHÈQUES » — chantier « Correctifs avant démo », point 6.
   *
   * Le même écran, deux phrases : l'agence REÇOIT des chèques qui ne lui
   * appartiennent pas, le vendeur lit les SIENS. Et il les lit seulement : les
   * trois gestes du cycle appartiennent à l'agence et au livreur, le service
   * les lui refuse déjà, et un bouton qui ne rend qu'une erreur est pire qu'un
   * bouton absent.
   *
   * Le RATTACHEMENT tranche, pas le rôle — comme partout ailleurs.
   */
  const merchant = user?.clientId !== null && user?.clientId !== undefined;

  return (
    <>
      <PageHeader
        title={merchant ? t('mineTitle') : t('title')}
        subtitle={merchant ? t('mineSubtitle') : t('subtitle')}
      />
      <ChequesScreen
        page={page}
        couriers={couriers}
        clients={clients}
        readOnly={merchant}
      />
    </>
  );
}
