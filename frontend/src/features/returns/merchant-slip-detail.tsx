'use client';

import type { ReturnSlipDetail } from '@delivery/shared';
import { Download, FileText } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useTransition } from 'react';
import { toast } from 'sonner';
import { CellBadge } from '@/components/data-table/cells';
import { ExportExcelButton } from '@/components/export-excel-button';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ParcelStatusBadge } from '@/features/parcels/status-badge';
import { useReturnError } from '@/features/returns/use-return-error';
import { Link } from '@/i18n/navigation';
import { returnSlipExportAction } from '@/lib/actions/documents';
import { downloadSlipPdfAction } from '@/lib/actions/returns';
import { newestFirst } from '@/lib/newest-first';

/**
 * LA FICHE D'UN BON DE RETOUR, VUE PAR LE VENDEUR — chantier « Bons de retour
 * vendeur ». En lecture : ni ajout, ni remise, ni suppression, qui sont des
 * gestes d'agence. « Imprimer le bon » et « Exporter Excel » passent par les
 * mêmes routes que l'admin, bornées par la même portée (`slipWhere`).
 */
export function MerchantSlipDetail({ slip }: { slip: ReturnSlipDetail }) {
  const t = useTranslations('returns.merchant.slips');
  const tSlips = useTranslations('returns.slips');
  const tStatus = useTranslations('returns.slips.status');
  const tExport = useTranslations('routes.detail');
  const errorMessage = useReturnError();
  const format = useFormatter();
  const [pending, startTransition] = useTransition();

  const money = (cents: number) => format.number(cents / 100, 'currency');

  const printPdf = () => {
    startTransition(async () => {
      const result = await downloadSlipPdfAction(slip.code);
      if (!result.ok) {
        toast.error(errorMessage(result));
        return;
      }

      const bytes = Uint8Array.from(atob(result.data), (char) => char.charCodeAt(0));
      const url = URL.createObjectURL(new Blob([bytes], { type: 'application/pdf' }));
      const link = document.createElement('a');
      link.href = url;
      link.download = `${slip.code}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    });
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        {slip.status === 'delivered' ? (
          <CellBadge label={tStatus('delivered')} color="var(--chart-4)" />
        ) : (
          <CellBadge label={tStatus('draft')} color="var(--chart-3)" />
        )}

        <div className="ms-auto flex flex-wrap items-center gap-2">
          <Button
            size="sm"
            variant="outline"
            className="gap-1.5"
            disabled={pending}
            onClick={printPdf}
          >
            <Download className="size-4" />
            <span className="hidden sm:inline">{tSlips('pdf')}</span>
          </Button>
          <ExportExcelButton
            label={tExport('exportExcel')}
            filename={slip.code}
            disabled={pending || slip.items.length === 0}
            onExport={() => returnSlipExportAction(slip.code)}
          />
        </div>
      </div>

      <Card>
        <CardContent className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
          <Fact label={t('deliveredAt')}>
            {slip.deliveredAt
              ? format.dateTime(new Date(slip.deliveredAt), 'stamp')
              : t('notDelivered')}
          </Fact>
          <Fact label={t('deliveredBy')}>{slip.deliveredByName ?? '-'}</Fact>
          <Fact label={t('receivedBy')}>{slip.receivedByName ?? '-'}</Fact>
          <Fact label={t('totalFees')}>
            <span className="tabular-nums">{money(slip.totalFees)}</span>
          </Fact>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <FileText className="size-4" />
            {t('itemsTitle')}
            <span className="text-muted-foreground text-sm font-normal">
              {t('parcels', { count: slip.items.length })}
            </span>
          </CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-2">
          {slip.items.length === 0 ? (
            <p className="text-muted-foreground py-6 text-center text-sm">{t('noItems')}</p>
          ) : (
            newestFirst(slip.items).map((item) => (
              <div
                key={item.id}
                className="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-lg border p-3 text-sm"
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
        </CardContent>
      </Card>
    </div>
  );
}

function Fact({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-muted-foreground text-xs">{label}</span>
      <span className="font-medium">{children}</span>
    </div>
  );
}
