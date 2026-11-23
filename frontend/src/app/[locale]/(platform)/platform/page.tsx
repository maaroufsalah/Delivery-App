import { getFormatter, getTranslations } from 'next-intl/server';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { TenantsScreen } from '@/features/platform/tenants-screen';
import { getPlatformAudit, getTenants } from '@/lib/api/platform';
import { platformDomain } from '@/lib/platform-domain';

/**
 * La console — C8 bloc 4, D-050.
 *
 * Composant SERVEUR : la liste et le journal sont lus ici, avec les filtres
 * portés par l'URL, et descendent en props. Le rôle est déjà vérifié par le
 * layout du groupe ; l'API l'exige de toute façon à chaque appel.
 */
export default async function PlatformPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const one = (key: string) => {
    const value = params[key];
    return Array.isArray(value) ? value[0] : value;
  };

  const [tenants, audit, t] = await Promise.all([
    getTenants({
      page: Number(one('page') ?? 1),
      pageSize: Number(one('pageSize') ?? 25),
      search: one('search'),
      status: one('status') as never,
    }),
    // Le journal reste court sur cet écran : il est là pour dire ce qui vient
    // de se passer, pas pour être audité en profondeur.
    getPlatformAudit({ page: 1, pageSize: 12 }),
    getTranslations('platform'),
  ]);

  return (
    <div className="flex flex-col gap-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{t('tenants.title')}</h1>
        <p className="text-muted-foreground mt-1 text-sm">{t('subtitle')}</p>
      </div>

      <TenantsScreen page={tenants} platformDomain={platformDomain()} />

      <AuditCard entries={audit.items} />
    </div>
  );
}

/**
 * Le journal de la console.
 *
 * Append-only : rien ne s'y modifie ni ne s'y efface. C'est ce qui rend « se
 * connecter en tant que » acceptable — l'accès au contenu d'un client existe,
 * mais il laisse une trace nominative que personne ne peut retirer.
 */
async function AuditCard({
  entries,
}: {
  entries: Awaited<ReturnType<typeof getPlatformAudit>>['items'];
}) {
  const [t, format] = await Promise.all([getTranslations('platform.audit'), getFormatter()]);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{t('title')}</CardTitle>
        <CardDescription>{t('description')}</CardDescription>
      </CardHeader>

      <CardContent>
        {entries.length === 0 ? (
          <p className="text-muted-foreground py-6 text-center text-sm">{t('empty')}</p>
        ) : (
          <ul className="flex flex-col divide-y">
            {entries.map((entry) => (
              <li key={entry.id} className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5 py-2.5">
                <span className="font-mono text-xs font-medium">{entry.action}</span>
                <span className="text-sm">{entry.targetCompanyName}</span>
                <span className="text-muted-foreground ms-auto text-xs">
                  {entry.actorName} · {format.dateTime(new Date(entry.createdAt), 'stamp')}
                </span>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
