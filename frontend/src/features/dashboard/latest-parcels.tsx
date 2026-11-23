'use client';

import type { ParcelListItem } from '@delivery/shared';
import { ArrowRight } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo } from 'react';
import { CellMuted, CellNumber, CellStack } from '@/components/data-table/cells';
import { DataTable } from '@/components/data-table/data-table';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ParcelStatusBadge } from '@/features/parcels/status-badge';
import { Link, useRouter } from '@/i18n/navigation';

const helper = createDataTableColumnHelper<ParcelListItem>();

/**
 * Les 10 derniers colis. Même DataTable que la liste complète, en mode compact
 * et avec cinq colonnes : sur un tableau de bord, la ligne doit se lire d'un
 * coup d'œil, pas se parcourir.
 */
export function LatestParcels({
  parcels,
  seeAllHref = '/parcels',
}: {
  parcels: ParcelListItem[];
  /** « Tout voir » : la liste filtrée comme le tableau de bord (drill-down). */
  seeAllHref?: string;
}) {
  const t = useTranslations('dashboard.latest');
  const tColumns = useTranslations('parcels.columns');
  const format = useFormatter();
  const router = useRouter();

  const columns: DataTableColumns<ParcelListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('code', {
          header: tColumns('code'),
          meta: { width: 'w-40' },
          cell: (info) => <CellStack mono label={info.getValue()} />,
        }),
        helper.accessor('recipientName', {
          header: tColumns('recipient'),
          meta: { hideBelow: 'sm' },
          cell: (info) => (
            <CellStack label={info.getValue()} sub={info.row.original.destinationCityName} />
          ),
        }),
        helper.accessor('clientName', {
          header: tColumns('client'),
          meta: { hideBelow: 'lg' },
          cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
        }),
        helper.accessor('statusId', {
          header: tColumns('status'),
          sortFn: 'basic',
          cell: (info) => <ParcelStatusBadge statusId={info.getValue()} />,
        }),
        helper.accessor('codAmount', {
          header: tColumns('cod'),
          sortFn: 'basic',
          meta: { align: 'end', hideBelow: 'md' },
          cell: (info) => <CellNumber value={format.number(info.getValue() / 100, 'currency')} />,
        }),
      ]),
    [tColumns, format],
  );

  return (
    <Card className="gap-0 overflow-hidden p-0">
      <CardHeader className="flex flex-row items-center justify-between gap-2 px-4 py-4">
        <CardTitle>{t('title')}</CardTitle>
        <Button asChild variant="ghost" size="sm" className="gap-1.5">
          <Link href={seeAllHref}>
            {t('seeAll')}
            <ArrowRight className="flip-rtl size-4" aria-hidden />
          </Link>
        </Button>
      </CardHeader>
      <CardContent className="p-0">
        <DataTable
          compact
          data={parcels}
          columns={columns}
          pageSize={10}
          onRowClick={(parcel) => router.push(`/parcels/${parcel.code}`)}
        />
      </CardContent>
    </Card>
  );
}
