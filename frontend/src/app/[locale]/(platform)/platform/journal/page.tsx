import { getTranslations } from 'next-intl/server';
import { TechJournalScreen } from '@/features/platform/tech-journal-screen';
import { getTechAudit, getTenants } from '@/lib/api/platform';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

/**
 * Console > Journal technique — recette SpeedyTrans 4, point 5 a (D-134).
 *
 * Le journal BRUT d'une société à la fois : on la choisit, puis on lit. Le
 * rôle est vérifié par le layout du groupe ; l'API exige `platform.*`.
 */
export default async function PlatformJournalPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const companyId = firstParam(params.companyId) ?? null;
  const list = listQueryFrom(params, { sort: 'at', order: 'desc' });

  const [tenants, page, t] = await Promise.all([
    getTenants({ pageSize: 100 }),
    companyId
      ? getTechAudit({
          companyId,
          page: list.page,
          pageSize: list.pageSize,
          search: firstParam(params.search),
        })
      : Promise.resolve(null),
    getTranslations('platform.techJournal'),
  ]);

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{t('title')}</h1>
        <p className="text-muted-foreground mt-1 text-sm">{t('subtitle')}</p>
      </div>
      <TechJournalScreen
        page={page}
        companyId={companyId}
        tenants={tenants.items.map((tenant) => ({ id: tenant.id, label: tenant.name }))}
      />
    </div>
  );
}
