import {
  JOURNAL_ACTIONS,
  JOURNAL_CATEGORIES,
  type JournalAction,
  type JournalCategory,
} from '@delivery/shared';
import type { Metadata } from 'next';
import { getTimeZone, getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { JournalScreen } from '@/features/audit/journal-screen';
import { getCurrentUser } from '@/lib/api/auth';
import { getJournal } from '@/lib/api/audit';
import { getUserPage } from '@/lib/api/accounts';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';
import { localToday, shiftDay } from '@/lib/local-day';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('settings.journal');
  return { title: t('title') };
}

/**
 * La période nommée dans l'URL, résolue en jours de la SOCIÉTÉ (D-135) —
 * l'API borne ces jours dans le même fuseau.
 */
function periodRange(period: string | undefined, timeZone: string): { from?: string; to?: string } {
  const days = period === 'today' ? 0 : period === 'week' ? 6 : period === 'month' ? 29 : null;
  if (days === null) return {};
  const today = localToday(timeZone);
  return { from: shiftDay(today, -days), to: today };
}

/*
 * D-255 : `a` ou `a,b` — les valeurs inconnues tombent, les autres partent
 * telles quelles ; l'API lit la liste. Le type partagé n'en nomme qu'une.
 */
const someOf = <T extends string>(
  value: string | undefined,
  allowed: readonly T[],
): T | undefined => {
  const known = (value ?? '')
    .split(',')
    .filter((item) => (allowed as readonly string[]).includes(item));
  return known.length > 0 ? (known.join(',') as T) : undefined;
};

/**
 * Paramètres > Journal — C11 bloc 5 (D-064), rendu LISIBLE par la recette
 * SpeedyTrans 4, point 4 (D-133).
 *
 * `audit.read`, que le défaut ne donne qu'au SUPER_ADMIN — l'admin société.
 * L'onglet du menu est masqué sans la clé, et la page la revérifie : un écran
 * caché n'est pas un écran protégé, et l'URL se tape. L'API ferme de toute
 * façon ces routes au vendeur et au livreur.
 */
export default async function JournalPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const user = await getCurrentUser();

  /*
   * `notFound()` plutôt qu'un 403 : sans la clé, l'écran n'existe pas pour ce
   * compte. Dire « interdit » confirmerait qu'il y a quelque chose à voir.
   */
  if (!user?.permissions.includes('audit.read')) notFound();

  const params = await searchParams;
  const t = await getTranslations('settings.journal');
  const range = periodRange(firstParam(params.period), await getTimeZone());
  const list = listQueryFrom(params, { sort: 'at', order: 'desc' });

  const [page, users] = await Promise.all([
    getJournal({
      page: list.page,
      pageSize: list.pageSize,
      search: list.search,
      view: firstParam(params.view) === 'logins' ? 'logins' : undefined,
      category: someOf<JournalCategory>(firstParam(params.category), JOURNAL_CATEGORIES),
      action: someOf<JournalAction>(firstParam(params.action), JOURNAL_ACTIONS),
      userId: firstParam(params.userId),
      ...range,
    }),
    // Assez pour peupler un filtre : une société n'a pas deux cents comptes.
    getUserPage({ pageSize: 200 }),
  ]);

  return (
    <section className="flex flex-col gap-4">
      <div>
        <h2 className="text-lg font-semibold tracking-tight">{t('title')}</h2>
        <p className="text-muted-foreground mt-1 text-sm">{t('subtitle')}</p>
      </div>

      <JournalScreen
        technical={user.impersonated === true}
        page={page}
        exportQuery={Object.fromEntries(
          Object.entries(range).filter((entry): entry is [string, string] => Boolean(entry[1])),
        )}
        users={users.items.map((account) => ({
          id: account.id,
          // Le nom d'abord : c'est ainsi qu'il apparaît dans les lignes.
          label:
            `${account.firstName ?? ''} ${account.lastName ?? ''}`.trim() ||
            account.email ||
            account.username ||
            account.id,
        }))}
      />
    </section>
  );
}
