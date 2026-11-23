import {
  PETTY_CASH_KINDS,
  PETTY_CASH_STATUSES,
  type PettyCashKind,
  type PettyCashStatus,
} from '@delivery/shared';
import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { PettyCashScreen } from '@/features/petty-cash/petty-cash-screen';
import { getCurrentUser } from '@/lib/api/auth';
import { getPettyCashDays, getPettyCashMeta, getPettyCashPage } from '@/lib/api/petty-cash';
import { effectivePeriod } from '@/lib/dashboard-period';
import { readPeriodPreference } from '@/lib/dashboard-period.server';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('finance.pettyCash');
  return { title: t('title') };
}

/**
 * LA PETITE CAISSE — chantier « Caisse, OV, Petite caisse », point 3.
 *
 * La période est celle des tableaux de bord (URL, sinon cookie, sinon « ce
 * mois »), et elle borne ici la LISTE comme le bandeau : la page répond à
 * « qu'est-ce qui est sorti de la caisse ce mois-ci ? ».
 */
export default async function PettyCashPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('finance.pettyCash');

  const remembered = await readPeriodPreference();
  const period = effectivePeriod(firstParam(params.period), remembered.period);
  const customFrom = firstParam(params.from) ?? remembered.range?.from;
  const customTo = firstParam(params.to) ?? remembered.range?.to;

  // Une valeur inconnue dans l'URL est ignorée plutôt que renvoyée à l'API.
  const kind = firstParam(params.kind);
  const knownKind = (PETTY_CASH_KINDS as readonly string[]).includes(kind ?? '')
    ? (kind as PettyCashKind)
    : undefined;

  /*
   * D-255 — le statut, à choix multiple (`draft,refused`) : les valeurs
   * inconnues tombent, le reste part tel quel, l'API lit la liste. Il n'était
   * jusqu'ici jamais transmis : le filtre de l'écran ne filtrait rien.
   */
  const statuses = (firstParam(params.status) ?? '')
    .split(',')
    .filter((value) => (PETTY_CASH_STATUSES as readonly string[]).includes(value));
  // Le type partagé n'a qu'une valeur ; la liste n'est lue qu'à l'API (D-255).
  const status = statuses.length > 0 ? (statuses.join(',') as PettyCashStatus) : undefined;

  // D-252 — les journées (en-têtes du tableau unique et bandeau du jour), au
  // même filtre d'agence que la liste.
  const agencyId = firstParam(params.agencyId);

  const [page, meta, user, days] = await Promise.all([
    getPettyCashPage({
      ...listQueryFrom(params, { sort: 'date', order: 'desc' }),
      period,
      from: period === 'custom' ? customFrom : undefined,
      to: period === 'custom' ? customTo : undefined,
      agencyId,
      kind: knownKind,
      category: firstParam(params.category),
      buyerId: firstParam(params.buyerId),
      status,
    }),
    getPettyCashMeta(),
    getCurrentUser(),
    getPettyCashDays({ agencyId }),
  ]);

  const canWrite = user?.permissions.includes('pettycash.write') ?? false;

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <PettyCashScreen
        page={page}
        meta={meta}
        canWrite={canWrite}
        days={days}
        agencyId={agencyId}
      />
    </>
  );
}
