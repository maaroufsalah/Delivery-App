'use client';

import {
  type Client,
  type Courier,
  DELIVERY_SLIP_STATUS_COLORS,
  DELIVERY_SLIP_STATUSES,
  type DeliverySlipListItem,
  type DeliverySlipTarget,
  type Paginated,
} from '@delivery/shared';
import { Eye, Loader2, PackageCheck, Undo2, UserCheck } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useState } from 'react';
import { toast } from 'sonner';
import { CellBadge, CellMuted, CellStack, RowActions } from '@/components/data-table/cells';
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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { DropdownMenuItem } from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useRouter } from '@/i18n/navigation';
import { advanceDeliverySlipsAction, revertDeliverySlipAction } from '@/lib/actions/bl';

const helper = createDataTableColumnHelper<DeliverySlipListItem>();

/** Les filtres de l'écran, tels que l'API les attend — `filters` de « toute la liste ». */
const FILTER_KEYS = ['status', 'clientId', 'courierId', 'from', 'to'] as const;

/**
 * « BONS DE LIVRAISON » — lot E, E1.2 et E1.3 (D-201).
 *
 * La liste des BL, la même pour l'admin et pour le vendeur : c'est la portée
 * serveur qui ne rend au vendeur que les siens. Un BL y apparaît dès que le
 * livreur l'a récupéré à la livraison — aucune création manuelle.
 *
 * **Les gestes de l'agence sont en masse** : cocher, « Réceptionner (X) » ou
 * « Remettre au vendeur (X) », confirmer. La sélection est celle du socle
 * (`DataTableSelection`, « X cochés / Y ») ; « toute la liste » part en
 * critère, et c'est le serveur qui la résout. Un BL qui n'est pas à l'étape
 * est IGNORÉ par le serveur, et le message le dit.
 *
 * Le vendeur ne voit ni case ni geste (`canConfirm`) : l'API les lui refuse de
 * toute façon (`returns.confirm`).
 */
export function DeliverySlipsScreen({
  page,
  clients,
  couriers,
  canConfirm,
  canRevert,
  merchant,
}: {
  page: Paginated<DeliverySlipListItem>;
  clients: Client[];
  couriers: Courier[];
  /** `returns.confirm` : réceptionner, remettre au vendeur. */
  canConfirm: boolean;
  /** Administrateur : le retour arrière d'une étape, avec motif. */
  canRevert: boolean;
  merchant: boolean;
}) {
  const t = useTranslations('returns.deliverySlips');
  const tStatus = useTranslations('returns.bl.slipStatus');
  const tBl = useTranslations('returns.bl');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'collectedAt',
    defaultOrder: 'desc',
  });
  const [search, setSearch] = useUrlSearch(setFilter, param('search'));

  const [selected, setSelected] = useState<string[]>([]);
  const [allMatching, setAllMatching] = useState(false);
  const changeSelection = (next: string[]) => {
    setAllMatching(false);
    setSelected(next);
  };
  const selectedCount = allMatching ? page.total : selected.length;

  const [target, setTarget] = useState<DeliverySlipTarget | null>(null);
  const [saving, setSaving] = useState(false);
  const [reverting, setReverting] = useState<DeliverySlipListItem | null>(null);
  const [reason, setReason] = useState('');
  const [receivedBy, setReceivedBy] = useState('');

  /** Les filtres AFFICHÉS — ceux que la page passe à l'API, ni plus ni moins. */
  const listFilters = () => {
    const filters: Record<string, string> = {};
    for (const key of FILTER_KEYS) {
      const value = param(key);
      if (value) filters[key] = value;
    }
    if (search) filters.search = search;
    return filters;
  };

  const runAdvance = async () => {
    if (!target) return;
    setSaving(true);
    // « Remettre au vendeur » : le receveur, exigé par l’API (lot F, F4).
    const receiver = target === 'returned' ? { returnedToName: receivedBy.trim() } : {};
    const result = await advanceDeliverySlipsAction(
      allMatching
        ? { to: target, all: true, filters: listFilters(), ...receiver }
        : { to: target, ids: selected, ...receiver },
    );
    setSaving(false);

    if (!result.ok) {
      toast.error(result.error);
      return;
    }

    toast.success(t('bulk.done', { count: result.data.count }));
    if (result.data.skipped > 0) toast.info(t('bulk.skipped', { count: result.data.skipped }));
    setTarget(null);
    setReceivedBy('');
    // Vidée APRÈS le succès : un échec réseau n'oblige pas à tout recocher.
    changeSelection([]);
  };

  const runRevert = async () => {
    if (!reverting) return;
    setSaving(true);
    const result = await revertDeliverySlipAction(reverting.id, { reason });
    setSaving(false);

    if (!result.ok) {
      toast.error(result.error);
      return;
    }

    toast.success(t('revert.done', { status: tStatus(result.data.status) }));
    setReverting(null);
    setReason('');
  };

  const selects: FilterSelect[] = useMemo(
    () => [
      /*
       * Le statut reste à valeur unique (D-255) : « toute la liste » rejoue ce
       * filtre dans `deliverySlipAdvanceSchema`, qui l'attend en énumération.
       */
      {
        id: 'status',
        label: t('filters.state'),
        value: param('status') ?? ALL_VALUE,
        options: DELIVERY_SLIP_STATUSES.map((status) => ({
          value: status,
          label: tStatus(status),
          color: DELIVERY_SLIP_STATUS_COLORS[status],
        })),
        onChange: (value) => setFilter('status', value === ALL_VALUE ? undefined : value),
      },
      // Le vendeur n'a qu'un client — le sien : le filtre ne lui apprendrait rien.
      ...(merchant
        ? []
        : [
            {
              id: 'clientId',
              multiple: true,
              label: t('filters.client'),
              value: param('clientId') ?? ALL_VALUE,
              // Seuls les Pro : les autres n'ont aucun BL, par construction (D-099).
              options: clients
                .filter((client) => client.isPro)
                .map((client) => ({ value: client.id, label: client.name })),
              onChange: (value: string) =>
                setFilter('clientId', value === ALL_VALUE ? undefined : value),
            },
          ]),
      {
        id: 'courierId',
        multiple: true,
        label: t('filters.courier'),
        value: param('courierId') ?? ALL_VALUE,
        options: couriers.map((courier) => ({
          value: courier.id,
          label: `${courier.firstName} ${courier.lastName}`,
        })),
        onChange: (value) => setFilter('courierId', value === ALL_VALUE ? undefined : value),
      },
    ],
    [clients, couriers, merchant, param, setFilter, t, tStatus],
  );

  const columns: DataTableColumns<DeliverySlipListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('parcelCode', {
          header: t('columns.parcel'),
          cell: (info) => (
            <CellStack
              label={info.getValue()}
              sub={info.row.original.reference ?? t('noReference')}
              mono
            />
          ),
        }),

        helper.accessor('clientName', {
          header: t('columns.client'),
          meta: { hideBelow: 'md' },
          cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
        }),

        helper.accessor('courierName', {
          header: t('columns.courier'),
          meta: { hideBelow: 'lg' },
          cell: (info) => <CellMuted>{info.getValue() ?? '-'}</CellMuted>,
        }),

        helper.accessor('deliveredAt', {
          header: t('columns.deliveredAt'),
          meta: { hideBelow: 'lg' },
          cell: (info) => {
            const value = info.getValue();
            return <CellMuted>{value ? format.dateTime(new Date(value), 'stamp') : '-'}</CellMuted>;
          },
        }),

        helper.accessor('collectedAt', {
          header: t('columns.collectedAt'),
          meta: { hideBelow: 'md' },
          cell: (info) => {
            const value = info.getValue();
            return <CellMuted>{value ? format.dateTime(new Date(value), 'stamp') : '-'}</CellMuted>;
          },
        }),

        helper.accessor('status', {
          header: t('columns.state'),
          meta: { width: 'w-48' },
          cell: (info) => {
            const status = info.row.original.status;
            return (
              <CellBadge label={tStatus(status)} color={DELIVERY_SLIP_STATUS_COLORS[status]} />
            );
          },
        }),

        helper.display({
          id: 'actions',
          header: '',
          meta: { isActions: true, align: 'end', width: 'w-16' },
          cell: (info) => {
            const row = info.row.original;

            return (
              <RowActions>
                <DropdownMenuItem onClick={() => router.push(`/parcels/${row.parcelCode}`)}>
                  <Eye className="size-4" />
                  {t('openParcel')}
                </DropdownMenuItem>
                {canRevert ? (
                  <DropdownMenuItem
                    disabled={row.status === 'collected' || row.status === 'closed'}
                    onClick={() => {
                      setReason('');
                      setReverting(row);
                    }}
                  >
                    <Undo2 className="flip-rtl size-4" />
                    {t('revert.action')}
                  </DropdownMenuItem>
                ) : null}
              </RowActions>
            );
          },
        }),
      ]),
    [canRevert, format, router, t, tStatus],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(row) => router.push(`/parcels/${row.parcelCode}`)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        selection={
          canConfirm
            ? {
                keyOf: (row) => row.id,
                selected,
                onChange: changeSelection,
                // « X cochés / Y » (D-176) : Y est la liste entière, toutes pages.
                total: page.total,
                count: selectedCount,
                actions: (
                  <>
                    {allMatching ? (
                      <span className="text-muted-foreground text-xs">
                        {t('bulk.allSelected', { count: String(page.total) })}
                      </span>
                    ) : page.total > selected.length ? (
                      <Button size="sm" variant="ghost" onClick={() => setAllMatching(true)}>
                        {t('bulk.selectAll', { count: String(page.total) })}
                      </Button>
                    ) : null}
                    <Button size="sm" variant="outline" onClick={() => setTarget('received')}>
                      <PackageCheck className="size-4" aria-hidden />
                      {t('bulk.receive', { count: String(selectedCount) })}
                    </Button>
                    <Button size="sm" variant="outline" onClick={() => setTarget('returned')}>
                      <UserCheck className="size-4" aria-hidden />
                      {t('bulk.return', { count: String(selectedCount) })}
                    </Button>
                  </>
                ),
              }
            : undefined
        }
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="bl/delivery-slips"
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            onReset={() => {
              setSearch('');
              changeSelection([]);
              reset();
            }}
          >
            {/* La période — deux dates crues dans l'URL, comme la liste des chèques. */}
            <div className="col-span-2 grid grid-cols-2 gap-2 md:col-span-1 md:flex md:items-center">
              <Input
                type="date"
                value={param('from') ?? ''}
                aria-label={t('filters.from')}
                className="h-9 md:w-40"
                onChange={(event) => setFilter('from', event.target.value || undefined)}
              />
              <Input
                type="date"
                value={param('to') ?? ''}
                aria-label={t('filters.to')}
                className="h-9 md:w-40"
                onChange={(event) => setFilter('to', event.target.value || undefined)}
              />
            </div>
          </DataTableToolbar>
        }
      />

      {target ? (
        <Dialog open onOpenChange={(next) => (next || saving ? null : setTarget(null))}>
          <DialogContent className="sm:max-w-md">
            <DialogHeader>
              <DialogTitle>
                {target === 'received'
                  ? t('bulk.receiveTitle', { count: String(selectedCount) })
                  : t('bulk.returnTitle', { count: String(selectedCount) })}
              </DialogTitle>
              <DialogDescription>
                {target === 'received' ? t('bulk.receiveHint') : t('bulk.returnHint')}
              </DialogDescription>
            </DialogHeader>
            {/* Le nom de qui signe chez le vendeur — même champ que le bon BRB- (lot F, F4). */}
            {target === 'returned' ? (
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="bl-received-by">{tBl('returnedToName')}</Label>
                <Input
                  id="bl-received-by"
                  value={receivedBy}
                  maxLength={120}
                  onChange={(event) => setReceivedBy(event.target.value)}
                />
              </div>
            ) : null}
            <DialogFooter className="gap-2 sm:gap-2">
              <Button variant="outline" onClick={() => setTarget(null)} disabled={saving}>
                {tCommon('cancel')}
              </Button>
              <Button
                onClick={() => void runAdvance()}
                disabled={saving || (target === 'returned' && receivedBy.trim().length < 2)}
                className="gap-1.5"
              >
                {saving ? <Loader2 className="size-4 animate-spin" aria-hidden /> : null}
                {t('bulk.confirm')}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      ) : null}

      {reverting ? (
        <Dialog open onOpenChange={(next) => (next || saving ? null : setReverting(null))}>
          <DialogContent className="sm:max-w-md">
            <DialogHeader>
              <DialogTitle>{t('revert.title')}</DialogTitle>
              <DialogDescription>{t('revert.hint')}</DialogDescription>
            </DialogHeader>
            <p className="font-mono text-sm">
              {reverting.parcelCode} · {tStatus(reverting.status)}
            </p>
            <Textarea
              value={reason}
              aria-label={t('revert.reason')}
              placeholder={t('revert.reason')}
              onChange={(event) => setReason(event.target.value)}
            />
            <DialogFooter className="gap-2 sm:gap-2">
              <Button variant="outline" onClick={() => setReverting(null)} disabled={saving}>
                {tCommon('cancel')}
              </Button>
              <Button
                variant="destructive"
                onClick={() => void runRevert()}
                disabled={saving || reason.trim().length === 0}
              >
                {t('revert.confirm')}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      ) : null}
    </>
  );
}
