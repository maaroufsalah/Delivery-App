import type { Metadata } from 'next';
import { getFormatter, getTranslations } from 'next-intl/server';
import { Card, CardContent } from '@/components/ui/card';
import { CellBadge } from '@/components/data-table/cells';
import { ChequePhotoUpload } from '@/features/courier/cheque-photo-upload';
import { Link } from '@/i18n/navigation';
import { getCourierStatementPage } from '@/lib/api/finance';
import { cn } from '@/lib/utils';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courier.statements');
  return { title: t('title') };
}

const TONE: Record<string, string> = {
  draft: 'var(--chart-3)',
  validated: 'var(--chart-1)',
  paid: 'var(--chart-4)',
};

/**
 * Mes relevés — C11 bloc 7, D-068.
 *
 * Le livreur y trouve ce que le bloc 6 a rendu lisible : ce qu'il a gagné, ce
 * qu'il a déjà gardé aux remises, et ce qui reste. Les trois montants sont
 * empilés plutôt que mis en colonnes — sur un téléphone, trois colonnes de
 * chiffres deviennent trois colonnes illisibles.
 *
 * C'est aussi d'ici qu'il PHOTOGRAPHIE son chèque, quand il en a reçu un.
 *
 * **LE FILTRE VIENT DE LA FACTURATION** — L2 bloc 9. Ses lignes « Paiements
 * reçus » et « Paiements en attente » ouvrent cet écran-ci : sans le filtre,
 * les deux menaient à la même liste entière, et un livreur qui vient de lire
 * « 2 en attente » aurait eu à les chercher parmi douze. Il reste modifiable à
 * l'écran — on arrive avec une question, on repart souvent avec une autre — et
 * il vit dans l'URL, comme partout ici.
 */
const FILTERS = ['all', 'paid', 'pending'] as const;
type Filter = (typeof FILTERS)[number];

export default async function CourierStatementsPage({
  searchParams,
}: {
  searchParams: Promise<{ filter?: string }>;
}) {
  const { filter: raw } = await searchParams;
  const filter: Filter = FILTERS.includes(raw as Filter) ? (raw as Filter) : 'all';

  const [t, tStatus, format, statements] = await Promise.all([
    getTranslations('courier.statements'),
    getTranslations('finance.statements.status'),
    getFormatter(),
    getCourierStatementPage({ pageSize: 25 }),
  ]);

  /*
   * Le filtre s'applique ICI et non dans la requête : `GET /courier-statements`
   * n'a pas de paramètre « payé », et lui en ajouter un pour vingt-cinq lignes
   * déjà chargées serait un endpoint de plus pour rien.
   */
  const shown = statements.items.filter((statement) =>
    filter === 'all'
      ? true
      : filter === 'paid'
        ? statement.paidAt !== null
        : statement.paidAt === null,
  );

  const money = (cents: number) => format.number(cents / 100, 'currency');

  return (
    <section className="flex flex-col gap-3">
      <h1 className="text-lg font-semibold tracking-tight">{t('title')}</h1>

      <nav className="flex flex-wrap gap-2" aria-label={t('title')}>
        {FILTERS.map((key) => (
          <Link
            key={key}
            href={key === 'all' ? '/me/statements' : `/me/statements?filter=${key}`}
            aria-current={filter === key ? 'page' : undefined}
            className={cn(
              'focus-visible:ring-ring flex min-h-10 items-center rounded-full border px-4 text-sm transition-colors outline-none focus-visible:ring-2',
              filter === key
                ? 'border-primary bg-primary text-primary-foreground font-medium'
                : 'hover:bg-muted text-muted-foreground',
            )}
          >
            {t(key)}
          </Link>
        ))}
      </nav>

      {shown.length === 0 ? (
        <p className="text-muted-foreground py-12 text-center text-sm">{t('empty')}</p>
      ) : null}

      <ul className="flex flex-col gap-3">
        {shown.map((statement) => (
          <li key={statement.id}>
            <Card>
              <CardContent className="flex flex-col gap-3 p-4">
                <span className="flex items-start gap-3">
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-mono text-sm font-medium">
                      {statement.code}
                    </span>
                    <span className="text-muted-foreground block truncate text-xs">
                      {t('period', {
                        start: format.dateTime(new Date(statement.periodStart), 'short'),
                        end: format.dateTime(new Date(statement.periodEnd), 'short'),
                      })}
                    </span>
                  </span>

                  <CellBadge
                    label={tStatus(statement.status)}
                    color={TONE[statement.status] ?? 'var(--chart-3)'}
                  />
                </span>

                {/*
                  Les trois montants du bloc 6, empilés. « Déjà gardées » n'est
                  affichée que si elle existe : sur un relevé de livreur salarié
                  elle vaut zéro, et une ligne à zéro pose une question au lieu
                  d'y répondre.
                */}
                <dl className="flex flex-col gap-1 text-sm">
                  <div className="flex items-baseline justify-between gap-3">
                    <dt className="text-muted-foreground">{t('commissions')}</dt>
                    <dd className="tabular-nums">{money(statement.commissionsTotal)}</dd>
                  </div>

                  {statement.commissionsDeducted > 0 ? (
                    <div className="flex items-baseline justify-between gap-3">
                      <dt className="text-muted-foreground">{t('kept')}</dt>
                      <dd className="tabular-nums">-{money(statement.commissionsDeducted)}</dd>
                    </div>
                  ) : null}

                  <div className="flex items-baseline justify-between gap-3 border-t pt-1">
                    <dt className="font-medium">{t('due')}</dt>
                    <dd className="font-semibold tabular-nums">{money(statement.totalDue)}</dd>
                  </div>
                </dl>

                {/*
                  Le chèque ne se photographie qu'une fois le relevé PAYÉ : une
                  photo attachée à un relevé qui n'a rien réglé ressemble à une
                  preuve, et n'en est pas une.
                */}
                {statement.status === 'paid' ? (
                  <ChequePhotoUpload code={statement.code} hasPhoto={false} />
                ) : null}
              </CardContent>
            </Card>
          </li>
        ))}
      </ul>
    </section>
  );
}
