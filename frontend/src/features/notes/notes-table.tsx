'use client';

import {
  type Agency,
  type City,
  type Client,
  type DeliveryNoteListItem,
  type DeliveryNoteStats,
  NOTE_STATUS_COLORS,
  NOTE_STATUSES,
  type NoteKind,
  type NoteStatus,
  type Paginated,
} from '@delivery/shared';
import { ClipboardCheck, ClipboardList, Package, Plus } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useCallback, useMemo, useState } from 'react';
import { CellBadge, CellMuted, CellNumber, CellStack } from '@/components/data-table/cells';
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
import { KpiCard, KpiGrid } from '@/components/kpi-card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { NoteComposer } from '@/features/notes/note-composer';
import { NoteForm } from '@/features/notes/note-form';
import { useRouter } from '@/i18n/navigation';
import { requestDropoffNoteAction, requestPickupNoteAction } from '@/lib/actions/routes';

const helper = createDataTableColumnHelper<DeliveryNoteListItem>();

/**
 * Bons de ramassage et bons de dépôt — DOMAIN.md §1.12.
 *
 * UNE table pour les deux : le legacy n'a qu'une table `bon_livraisons`
 * discriminée par `type`, et les deux objets ont exactement les mêmes colonnes.
 * Seuls le titre, la route et le vocabulaire changent.
 */
export function NotesTable({
  page,
  stats,
  clients,
  agencies,
  cities,
  kind,
  selfClientId = null,
}: {
  page: Paginated<DeliveryNoteListItem>;
  /** Compteurs de la SÉLECTION, calculés par l'API (C7 bloc 4). */
  stats: DeliveryNoteStats;
  clients: Client[];
  agencies: Agency[];
  cities: City[];
  kind: NoteKind;
  /**
   * LE VENDEUR NE COMPOSE PAS, IL DEMANDE — retouches Anass, points 2 et 3.
   *
   * Posé, ce `clientId` change deux choses. Le filtre « E-commerçant » part :
   * il n'a qu'une valeur possible, et c'est lui-même. Et « Nouveau bon »
   * n'ouvre plus le formulaire de l'administration — celui-ci poste sur
   * `POST /delivery-notes`, gardé par `notes.write`, que le vendeur n'a pas et
   * ne doit pas avoir : cette route prend un `clientId` dans le corps sans le
   * confronter à la portée (D-072). Le bouton rendait donc « Permission
   * insuffisante pour cette opération », à chaque essai, sur les deux genres
   * de bon.
   *
   * À la place, il coche ses colis et `POST /merchant/me/{pickups,dropoffs}`
   * ouvre le bon EN ATTENTE, sur le client de la session. Les deux routes
   * existaient depuis V2 vendeur, et les Server Actions aussi : c'est l'écran
   * web qui ne les avait jamais appelées. L'application mobile, elle, passait
   * déjà par là.
   */
  selfClientId?: string | null;
}) {
  const t = useTranslations('notes');
  const tDetail = useTranslations('notes.detail');
  const tPick = useTranslations('candidates');
  const tStatus = useTranslations('noteStatus');
  const format = useFormatter();

  const router = useRouter();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'createdAt',
    defaultOrder: 'desc',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [creating, setCreating] = useState(false);

  const basePath = kind === 'pickup' ? '/pickup-notes' : '/dropoff-notes';

  // `useCallback` : la référence entre dans les dépendances du `useMemo` des
  // filtres ; recréée à chaque rendu, elle le recalculerait à chaque frappe.
  const setParam = useCallback(
    (key: string, value: string) => setFilter(key, value === ALL_VALUE ? undefined : value),
    [setFilter],
  );

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        id: 'status',
        multiple: true,
        label: t('filters.status'),
        value: param('status') ?? ALL_VALUE,
        options: NOTE_STATUSES.map((status) => ({
          value: status,
          label: tStatus(status),
          color: NOTE_STATUS_COLORS[status],
        })),
        onChange: (value) => setParam('status', value),
      },
      // Un vendeur ne filtre pas sur lui-même : la portée serveur l'a déjà fait.
      ...(selfClientId
        ? []
        : [
            {
              id: 'clientId',
              multiple: true,
              label: t('filters.client'),
              value: param('clientId') ?? ALL_VALUE,
              options: clients.map((client) => ({ value: client.id, label: client.name })),
              onChange: (value: string) => setParam('clientId', value),
            },
          ]),
    ],
    [clients, param, selfClientId, setParam, t, tStatus],
  );

  const columns: DataTableColumns<DeliveryNoteListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('code', {
          header: t('columns.code'),
          meta: { width: 'w-40' },
          cell: (info) => <CellStack mono label={info.getValue()} />,
        }),
        helper.accessor('clientName', {
          header: t('columns.client'),
          cell: (info) => <CellStack label={info.getValue()} sub={info.row.original.cityName} />,
        }),
        helper.accessor('parcelCount', {
          header: t('columns.parcels'),
          sortFn: 'basic',
          meta: { align: 'end' },
          /*
           * « ramassés X/Y » — Recette 5, points 7 et 8 : le total compte les
           * colis retirés (non remis), et la ligne dit combien sont ramassés.
           */
          cell: (info) => {
            const row = info.row.original;
            const total = row.parcelCount + row.removedCount;
            return (
              <CellNumber
                value={`${format.number(row.pickedCount, 'integer')} / ${format.number(total, 'integer')}`}
                sub={
                  row.pickupWaived
                    ? t('waived')
                    : row.removedCount > 0
                      ? tDetail('removedCount', { count: row.removedCount })
                      : undefined
                }
              />
            );
          },
        }),
        helper.accessor('status', {
          header: t('columns.status'),
          cell: (info) => {
            // v9 élargit la valeur d'accesseur à `any` : on la resserre ici,
            // sinon l'indexation de la table de couleurs n'est plus vérifiée.
            const status = info.getValue() as NoteStatus;
            return <CellBadge label={tStatus(status)} color={NOTE_STATUS_COLORS[status]} />;
          },
        }),
        helper.accessor('createdAt', {
          header: t('columns.date'),
          sortFn: 'datetime',
          meta: { hideBelow: 'md' },
          cell: (info) => (
            <CellMuted>{format.dateTime(new Date(info.getValue()), 'stamp')}</CellMuted>
          ),
        }),
      ]),
    [t, tDetail, tStatus, format],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(note) => router.push(`${basePath}/${note.code}`)}
        emptyTitle={t(`empty.${kind}`)}
        kpis={
          <KpiGrid>
            <KpiCard
              label={t('kpi.total')}
              value={format.number(stats.total, 'integer')}
              icon={ClipboardList}
            />
            <KpiCard
              label={t('kpi.awaiting')}
              value={format.number(stats.awaiting, 'integer')}
              icon={ClipboardCheck}
              tone="var(--chart-2)"
            />
            <KpiCard
              label={t('kpi.parcels')}
              value={format.number(stats.parcels, 'integer')}
              icon={Package}
              tone="var(--chart-3)"
            />
            <KpiCard
              label={t('kpi.waived')}
              value={format.number(stats.waived, 'integer')}
              hint={t('kpi.waivedHint')}
              icon={ClipboardCheck}
              tone="var(--chart-4)"
            />
          </KpiGrid>
        }
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="delivery-notes"
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            onReset={() => {
              setSearch('');
              reset();
            }}
          >
            <Badge variant="secondary" className="rounded-full font-normal">
              {t(`kind.${kind}`)}
            </Badge>
            <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
              <Plus className="size-4" aria-hidden />
              <span className="hidden sm:inline">{t('form.new')}</span>
            </Button>
          </DataTableToolbar>
        }
      />

      {creating && selfClientId ? (
        <NoteComposer
          clientId={null}
          title={t(`request.title.${kind}`)}
          hint={
            kind === 'pickup'
              ? // A2-5 (D-182) : la règle telle que réglée, jamais un chiffre en dur ;
                // lot T (D-232) : la SIENNE (fiche, sinon global), gratuite si 0.
                stats.pickupFee === 0 || stats.freePickupThreshold === 0
                ? t('request.hint.pickupFree')
                : t('request.hint.pickup', {
                    threshold: String(stats.freePickupThreshold ?? 5),
                    fee: format.number((stats.pickupFee ?? 0) / 100, 'currency'),
                  })
              : t('request.hint.dropoff')
          }
          submitLabel={(count) => t('request.submit', { count: String(count) })}
          successMessage={(count) => `${t('request.done')} · ${tPick('requested', { count })}`}
          onSubmit={async (selection) => {
            const result =
              kind === 'pickup'
                ? await requestPickupNoteAction(selection)
                : await requestDropoffNoteAction(selection);

            if (result.ok) router.push(`${basePath}/${result.data.code}`);
            return result;
          }}
          onDone={() => setCreating(false)}
        />
      ) : null}

      {creating && !selfClientId ? (
        <NoteForm
          kind={kind}
          clients={clients}
          agencies={agencies}
          cities={cities}
          onDone={() => setCreating(false)}
          onCreated={(code) => router.push(`${basePath}/${code}`)}
        />
      ) : null}
    </>
  );
}
