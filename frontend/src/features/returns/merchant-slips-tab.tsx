'use client';

import type { Paginated, ReturnSlipDetail, ReturnSlipListItem } from '@delivery/shared';
import { ChevronRight, FileText, Loader2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { CellBadge } from '@/components/data-table/cells';
import { DataTablePagination } from '@/components/data-table/data-table-pagination';
import { DataTableToolbar } from '@/components/data-table/data-table-toolbar';
import { useServerTable, useUrlSearch } from '@/components/data-table/use-server-table';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { ParcelStatusBadge } from '@/features/parcels/status-badge';
import { Link } from '@/i18n/navigation';
import { slipDetailAction } from '@/lib/actions/returns';
import { cn } from '@/lib/utils';

/**
 * L'ONGLET « BONS DE RETOUR » DU VENDEUR — chantier « Bons de retour vendeur ».
 *
 * Ses BRT-, une page à la fois : la page, la recherche et la période sont dans
 * l'URL et la page serveur demande à l'API la bonne tranche. Le vendeur n'est
 * JAMAIS un paramètre : le serveur le lit sur le compte (`slipWhere`).
 *
 * Les colis d'un bon ne descendent pas avec la liste : ils sont lus à
 * l'ouverture de SON accordéon, puis gardés — un bon de trois cents colis ne
 * pèse que s'il est ouvert.
 */
export function MerchantSlipsTab({ page }: { page: Paginated<ReturnSlipListItem> }) {
  const t = useTranslations('returns.merchant.slips');

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'createdAt',
    defaultOrder: 'desc',
  });
  const [search, setSearch] = useUrlSearch(setFilter, param('search'));

  // `DataTablePagination` lit une table ; la liste en accordéon lui prête la même forme.
  const pageCount = Math.max(1, Math.ceil(server.total / server.pageSize));
  const pager = {
    state: { pagination: { pageIndex: server.page - 1, pageSize: server.pageSize } },
    getRowCount: () => server.total,
    getCanPreviousPage: () => server.page > 1,
    getCanNextPage: () => server.page < pageCount,
    firstPage: () => server.onChange({ page: 1 }),
    previousPage: () => server.onChange({ page: server.page - 1 }),
    nextPage: () => server.onChange({ page: server.page + 1 }),
    lastPage: () => server.onChange({ page: pageCount }),
    setPageSize: (size: number) => server.onChange({ pageSize: size, page: 1 }),
  };

  return (
    <div className="flex flex-col gap-4">
      <DataTableToolbar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder={t('searchPlaceholder')}
        onReset={() => {
          setSearch('');
          reset();
        }}
      >
        <div className="col-span-2 grid grid-cols-2 gap-2 md:col-span-1 md:flex md:items-center">
          <Input
            type="date"
            value={param('from') ?? ''}
            aria-label={t('from')}
            className="h-9 md:w-40"
            onChange={(event) => setFilter('from', event.target.value || undefined)}
          />
          <Input
            type="date"
            value={param('to') ?? ''}
            aria-label={t('to')}
            className="h-9 md:w-40"
            onChange={(event) => setFilter('to', event.target.value || undefined)}
          />
        </div>
      </DataTableToolbar>

      <Card className="gap-0 overflow-hidden py-0">
        {page.items.length === 0 ? (
          <CardContent className="flex flex-col items-center gap-2 py-12 text-center">
            <FileText className="text-muted-foreground size-8" />
            <p className="font-medium">{t('empty')}</p>
            <p className="text-muted-foreground text-sm">{t('emptyHint')}</p>
          </CardContent>
        ) : (
          <div className="divide-y">
            {page.items.map((slip) => (
              <SlipRow key={slip.id} slip={slip} />
            ))}
          </div>
        )}
        {server.total > 0 ? <DataTablePagination table={pager} /> : null}
      </Card>
    </div>
  );
}

function SlipStateBadge({ status }: { status: ReturnSlipListItem['status'] }) {
  const tStatus = useTranslations('returns.slips.status');
  return status === 'delivered' ? (
    <CellBadge label={tStatus('delivered')} color="var(--chart-4)" />
  ) : (
    <CellBadge label={tStatus('draft')} color="var(--chart-3)" />
  );
}

function SlipRow({ slip }: { slip: ReturnSlipListItem }) {
  const t = useTranslations('returns.merchant.slips');
  const format = useFormatter();
  const [open, setOpen] = useState(false);
  const [detail, setDetail] = useState<ReturnSlipDetail | null>(null);
  const [failed, setFailed] = useState(false);
  const [loading, startLoading] = useTransition();

  const toggle = () => {
    const next = !open;
    setOpen(next);
    if (!next || detail) return;

    setFailed(false);
    startLoading(async () => {
      const result = await slipDetailAction(slip.code);
      if (result.ok) setDetail(result.data);
      else setFailed(true);
    });
  };

  const money = (cents: number) => format.number(cents / 100, 'currency');

  return (
    <div>
      <button
        type="button"
        onClick={toggle}
        aria-expanded={open}
        aria-label={t('toggle', { code: slip.code })}
        className="hover:bg-muted/50 flex w-full items-start gap-3 px-4 py-3 text-start text-sm"
      >
        <ChevronRight
          className={cn(
            'text-muted-foreground mt-0.5 size-4 shrink-0 transition-transform',
            open ? 'rotate-90' : 'flip-rtl',
          )}
        />
        <div className="grid min-w-0 flex-1 grid-cols-2 gap-x-4 gap-y-1 md:grid-cols-[minmax(0,1.4fr)_minmax(0,1.2fr)_minmax(0,1fr)_auto_auto_auto] md:items-center">
          <span className="font-mono text-[13px] font-medium tracking-tight">{slip.code}</span>
          <span className="justify-self-end md:order-last">
            <SlipStateBadge status={slip.status} />
          </span>
          <span className="text-muted-foreground text-xs">
            {slip.deliveredAt
              ? `${t('deliveredAt')} ${format.dateTime(new Date(slip.deliveredAt), 'stamp')}`
              : t('notDelivered')}
          </span>
          <span className="text-muted-foreground truncate text-xs">
            {slip.deliveredByName ? `${t('deliveredBy')} ${slip.deliveredByName}` : ''}
          </span>
          <span className="text-xs">{t('parcels', { count: slip.parcelCount })}</span>
          <span className="text-end text-xs tabular-nums md:text-start">
            {money(slip.totalFees)}
          </span>
        </div>
      </button>

      {open ? (
        <div className="bg-muted/20 flex flex-col gap-2 border-t px-4 py-3 ps-11">
          {loading ? (
            <p className="text-muted-foreground flex items-center gap-2 text-sm">
              <Loader2 className="size-4 animate-spin" />
              {t('loading')}
            </p>
          ) : failed ? (
            <p className="text-destructive text-sm">{t('loadError')}</p>
          ) : detail ? (
            <>
              {detail.items.length === 0 ? (
                <p className="text-muted-foreground text-sm">{t('noItems')}</p>
              ) : (
                detail.items.map((item) => (
                  <div
                    key={item.id}
                    className="bg-card flex flex-wrap items-center gap-x-4 gap-y-1 rounded-lg border p-2.5 text-sm"
                  >
                    <Link
                      href={`/parcels/${encodeURIComponent(item.parcelCode)}`}
                      className="font-mono text-[13px] font-medium tracking-tight hover:underline"
                    >
                      {item.parcelCode}
                    </Link>
                    <span className="min-w-0 flex-1 truncate">
                      {item.recipientName}
                      <span className="text-muted-foreground"> · {item.destinationCityName}</span>
                    </span>
                    {item.statusId !== undefined ? (
                      <ParcelStatusBadge statusId={item.statusId} />
                    ) : null}
                    <span className="shrink-0 text-xs tabular-nums">{money(item.feeApplied)}</span>
                  </div>
                ))
              )}
              <div>
                <Button asChild size="sm" variant="outline">
                  <Link href={`/returns/pending/slips/${encodeURIComponent(slip.code)}`}>
                    {t('open')}
                  </Link>
                </Button>
              </div>
            </>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
