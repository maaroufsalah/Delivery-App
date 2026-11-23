import { getTranslations } from 'next-intl/server';
import { ServerErrorsScreen } from '@/features/platform/server-errors-screen';
import { getServerErrors } from '@/lib/api/platform';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

/**
 * Console > Erreurs — recette SpeedyTrans 4, point 5 c (D-134).
 *
 * Le rôle est vérifié par le layout du groupe ; l'API exige `platform.*` à
 * chaque appel de toute façon.
 */
export default async function PlatformErrorsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const list = listQueryFrom(params, { sort: 'lastAt', order: 'desc' });
  const [page, t] = await Promise.all([
    getServerErrors({
      page: list.page,
      pageSize: list.pageSize,
      search: firstParam(params.search),
    }),
    getTranslations('platform.serverErrors'),
  ]);

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{t('title')}</h1>
        <p className="text-muted-foreground mt-1 text-sm">{t('subtitle')}</p>
      </div>
      <ServerErrorsScreen page={page} />
    </div>
  );
}
