'use client';

import type { CompanyStatus, Paginated, TenantListItem } from '@delivery/shared';
import { COMPANY_STATUSES } from '@delivery/shared';
import { CalendarPlus, CircleCheck, LogIn, Pause, Play, Plus } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellBadge, CellNumber, CellStack, RowActions } from '@/components/data-table/cells';
import { DataTable } from '@/components/data-table/data-table';
import {
  ALL_VALUE,
  DataTableToolbar,
  type FilterSelect,
} from '@/components/data-table/data-table-toolbar';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { useServerTable, useUrlSearch } from '@/components/data-table/use-server-table';
import { Button } from '@/components/ui/button';
import { DropdownMenuItem, DropdownMenuSeparator } from '@/components/ui/dropdown-menu';
import { CreateTenantDialog } from '@/features/platform/create-tenant-dialog';
import { ExtendTrialDialog } from '@/features/platform/extend-trial-dialog';
import { TenantConfirmDialog, type TenantAction } from '@/features/platform/tenant-confirm-dialog';
import { useRouter } from '@/i18n/navigation';
import { impersonateTenantAction } from '@/lib/actions/platform';

const helper = createDataTableColumnHelper<TenantListItem>();

/**
 * La liste des espaces clients — C8 bloc 4, D-050.
 *
 * **On y lit des DÉCOMPTES, jamais du contenu.** Combien de colis, combien de
 * comptes, quand quelqu'un s'est connecté pour la dernière fois. Rien de ce
 * qu'un client a saisi ne transite par cet écran : l'API ne le rend pas, et
 * c'est délibéré — héberger la donnée de quelqu'un n'est pas y avoir accès. Le
 * seul chemin vers le contenu est « se connecter en tant que », qui ouvre une
 * session ordinaire et laisse une trace nominative au journal.
 *
 * La couleur du statut est la même grammaire que partout : vert pour ce qui va,
 * ambre pour ce qui approche d'une échéance, rouge pour ce qui est fermé.
 */
export function TenantsScreen({
  page,
  platformDomain,
}: {
  page: Paginated<TenantListItem>;
  /** `delivery.nocido.com` — sert à montrer l'hôte d'un espace. */
  platformDomain: string;
}) {
  const t = useTranslations('platform');
  const format = useFormatter();
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'createdAt',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [creating, setCreating] = useState(false);
  const [extending, setExtending] = useState<TenantListItem | null>(null);
  const [confirming, setConfirming] = useState<{
    tenant: TenantListItem;
    action: TenantAction;
  } | null>(null);

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        // D-255 : plusieurs statuts (essai, actif, suspendu).
        id: 'status',
        multiple: true,
        label: t('tenants.allStatuses'),
        value: param('status') ?? ALL_VALUE,
        options: COMPANY_STATUSES.map((status) => ({
          value: status,
          label: t(`status.${status}`),
        })),
        onChange: (value) => setFilter('status', value === ALL_VALUE ? undefined : value),
      },
    ],
    [param, setFilter, t],
  );

  /**
   * « Se connecter en tant que ».
   *
   * L'API trace l'accès puis rend le slug ; on emmène ensuite l'administrateur
   * sur l'hôte du tenant. Aucune session n'est fabriquée ici : les cookies sont
   * posés par hôte (C8 bloc 5), et un cookie du domaine de la plateforme ne
   * serait de toute façon pas lisible sur celui du client.
   */
  const impersonate = (tenant: TenantListItem) => {
    startTransition(async () => {
      const result = await impersonateTenantAction(tenant.id);

      if (!result.ok) {
        toast.error(result.error);
        return;
      }

      /*
       * Le jeton voyage dans l'URL parce qu'il franchit une frontière de
       * DOMAINE : les cookies sont liés à l'hôte, et la session doit naître sur
       * celui du tenant. Il ne vaut que deux minutes, et l'écran de connexion
       * l'échange immédiatement contre une vraie session.
       */
      const host = tenant.customDomain ?? `${result.data.slug}.${platformDomain}`;
      const url = `https://${host}/login?impersonation=${encodeURIComponent(result.data.token)}`;

      window.open(url, '_blank', 'noopener');
    });
  };

  const columns: DataTableColumns<TenantListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('name', {
          header: t('tenants.columns.name'),
          cell: (info) => (
            <CellStack
              label={info.getValue()}
              // L'HÔTE en sous-libellé, pas l'identifiant : c'est ce qu'on tape
              // dans un navigateur, et ce qu'on dicte au téléphone.
              sub={info.row.original.customDomain ?? `${info.row.original.slug}.${platformDomain}`}
            />
          ),
        }),

        helper.accessor('status', {
          header: t('tenants.columns.status'),
          meta: { width: 'w-32' },
          cell: (info) => {
            // L'assistant de colonnes rend `any` : on renomme le type ici, une
            // fois, plutôt que de le caster à chaque usage.
            const status = info.getValue() as CompanyStatus;

            return <CellBadge label={t(STATUS_LABEL[status])} color={STATUS_COLOR[status]} />;
          },
        }),

        helper.accessor('trialDaysLeft', {
          header: t('tenants.columns.trial'),
          meta: { width: 'w-36', hideBelow: 'sm' },
          cell: (info) => {
            const left = info.getValue();
            if (left === null)
              return <span className="text-muted-foreground">{t('tenants.noTrial')}</span>;

            /*
             * Zéro ou moins = terminé. On l'écrit en toutes lettres plutôt que
             * d'afficher « -3 jours restants », qui se lit comme un bug.
             */
            if (left <= 0) {
              return <CellBadge label={t('tenants.trialOver')} color="var(--destructive)" />;
            }

            return (
              <span className={left <= 10 ? 'font-medium text-amber-600 dark:text-amber-400' : ''}>
                {t('tenants.trialLeft', { count: left })}
              </span>
            );
          },
        }),

        helper.accessor('parcelCount', {
          header: t('tenants.columns.parcels'),
          meta: { align: 'end', hideBelow: 'md' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),

        helper.accessor('userCount', {
          header: t('tenants.columns.users'),
          meta: { align: 'end', hideBelow: 'md' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),

        helper.accessor('lastActivityAt', {
          header: t('tenants.columns.lastActivity'),
          meta: { hideBelow: 'lg' },
          cell: (info) => {
            const value = info.getValue();
            if (!value) return <span className="text-muted-foreground">{t('tenants.never')}</span>;

            return <span>{format.dateTime(new Date(value), 'stamp')}</span>;
          },
        }),

        helper.display({
          id: 'actions',
          header: '',
          meta: { isActions: true, align: 'end', width: 'w-16' },
          cell: (info) => {
            const tenant = info.row.original;

            return (
              <RowActions>
                <DropdownMenuItem
                  disabled={pending}
                  onClick={() => setConfirming({ tenant, action: 'impersonate' })}
                >
                  <LogIn className="size-4" />
                  {t('actions.impersonate')}
                </DropdownMenuItem>

                <DropdownMenuSeparator />

                <DropdownMenuItem onClick={() => setExtending(tenant)}>
                  <CalendarPlus className="size-4" />
                  {t('actions.extend')}
                </DropdownMenuItem>

                {tenant.status !== 'active' ? (
                  <DropdownMenuItem onClick={() => setConfirming({ tenant, action: 'activate' })}>
                    <CircleCheck className="size-4" />
                    {t('actions.activate')}
                  </DropdownMenuItem>
                ) : null}

                <DropdownMenuSeparator />

                {tenant.status === 'suspended' ? (
                  <DropdownMenuItem onClick={() => setConfirming({ tenant, action: 'reactivate' })}>
                    <Play className="size-4" />
                    {t('actions.reactivate')}
                  </DropdownMenuItem>
                ) : (
                  <DropdownMenuItem
                    variant="destructive"
                    onClick={() => setConfirming({ tenant, action: 'suspend' })}
                  >
                    <Pause className="size-4" />
                    {t('actions.suspend')}
                  </DropdownMenuItem>
                )}
              </RowActions>
            );
          },
        }),
      ]),
    [t, format, platformDomain, pending],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        emptyTitle={t('tenants.empty')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            searchPlaceholder={t('tenants.search')}
            selects={selects}
            onReset={() => {
              setSearch('');
              reset();
            }}
          >
            <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
              <Plus className="size-4" />
              <span className="hidden sm:inline">{t('actions.create')}</span>
            </Button>
          </DataTableToolbar>
        }
      />

      {creating ? (
        <CreateTenantDialog
          platformDomain={platformDomain}
          onDone={() => {
            setCreating(false);
            router.refresh();
          }}
        />
      ) : null}

      {extending ? (
        <ExtendTrialDialog
          tenant={extending}
          onDone={() => {
            setExtending(null);
            router.refresh();
          }}
        />
      ) : null}

      {confirming ? (
        <TenantConfirmDialog
          tenant={confirming.tenant}
          action={confirming.action}
          onImpersonate={impersonate}
          onDone={() => {
            setConfirming(null);
            router.refresh();
          }}
        />
      ) : null}
    </>
  );
}

/**
 * Les clés de libellé, écrites en TOUTES LETTRES.
 *
 * `t(\`status.${x}\`)` compile mais ne se type pas : next-intl ne sait alors
 * plus quelle clé est visée, ni si elle attend des paramètres. La table rend la
 * clé littérale, et une traduction manquante casse le build — ce qui est tout
 * l'intérêt du catalogue typé (CLAUDE.md §4).
 */
const STATUS_LABEL = {
  trial: 'status.trial',
  active: 'status.active',
  suspended: 'status.suspended',
} as const satisfies Record<CompanyStatus, string>;

/** Vert pour ce qui va, ambre pour l'essai en cours, rouge pour ce qui est fermé. */
const STATUS_COLOR: Record<CompanyStatus, string> = {
  trial: 'var(--chart-3)',
  active: 'var(--chart-4)',
  suspended: 'var(--destructive)',
};
