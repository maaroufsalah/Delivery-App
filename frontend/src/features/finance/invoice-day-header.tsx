'use client';

import {
  INVOICE_DRAFT_DAY,
  type InvoiceClientKind,
  type InvoiceDaySummary,
} from '@delivery/shared';
import { Download, Landmark, Loader2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useSearchParams } from 'next/navigation';
import { useState } from 'react';
import { toast } from 'sonner';
import { PrintButton } from '@/components/print-button';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useRouter } from '@/i18n/navigation';
import { exportListAction } from '@/lib/actions/documents';
import {
  createTransferOrderAction,
  downloadInvoiceDayPdfAction,
  downloadTransferOrderAction,
} from '@/lib/actions/finance';
import { downloadBase64 } from '@/lib/download';

/**
 * L'EN-TÊTE D'UN JOUR de la liste des factures — D-254, « Groupes générés »
 * de l'ancienne appli.
 *
 * Date, nombre de factures, colis, total, et le détail des règlements par
 * méthode quand il y en a. Trois gestes, chacun une fonction EXISTANTE bornée
 * au jour : le PDF des factures (leurs pages, telles quelles), l'export de la
 * liste (le même classeur, filtré sur le jour), l'ordre de virement à partir
 * de factures (refusé par l'API pour une facture déjà virée).
 *
 * Le jour rejoue les filtres de la liste (l'URL et la recherche) : ce que
 * l'en-tête compte, imprime et exporte est ce que la liste montre ce jour-là.
 */
export function InvoiceDayHeader({
  day,
  summary,
  kind,
  search,
  canWrite,
}: {
  day: string;
  /** Absent : jour hors des en-têtes chargés (rare : une page rechargée) — la date seule. */
  summary: InvoiceDaySummary | undefined;
  kind: InvoiceClientKind;
  search: string;
  canWrite: boolean;
}) {
  const t = useTranslations('finance.invoices.days');
  const format = useFormatter();
  const searchParams = useSearchParams();
  const [exporting, setExporting] = useState(false);
  const [transferring, setTransferring] = useState(false);

  const draft = day === INVOICE_DRAFT_DAY;
  const money = (cents: number) => format.number(cents / 100, 'currency');
  const label = draft ? t('drafts') : format.dateTime(new Date(`${day}T12:00:00Z`), 'date');

  /** Les filtres de la liste, bornés au jour (un brouillon : `status=draft`). */
  const dayQuery = (): Record<string, string> => {
    const query: Record<string, string> = {};
    for (const [key, value] of searchParams.entries()) {
      if (key !== 'page' && key !== 'pageSize') query[key] = value;
    }
    if (search) query.search = search;
    if (draft) query.status = 'draft';
    else query.issuedDay = day;
    return query;
  };

  const exportDay = async () => {
    setExporting(true);
    const result = await exportListAction(kind === 'pro' ? 'invoices-pro' : 'invoices', dayQuery());
    setExporting(false);

    if (!result.ok) {
      toast.error(result.error);
      return;
    }
    downloadBase64(
      result.data.file,
      `Factures-${draft ? 'brouillons' : day}.xlsx`,
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    );
    if (result.data.rows !== null) toast.success(t('exported', { count: result.data.rows }));
  };

  const transferable = summary?.transferableCodes ?? [];
  const payments = summary
    ? (
        [
          ['cash', summary.paidCash],
          ['cheque', summary.paidCheque],
          ['transferPaid', summary.paidTransfer],
        ] as const
      ).filter(([, value]) => value !== 0)
    : [];

  return (
    <div className="flex flex-col gap-2 xl:flex-row xl:items-center xl:justify-between">
      <div className="flex min-w-0 flex-col gap-1">
        <p className="flex flex-wrap items-center gap-x-3 gap-y-1">
          <span className="font-semibold">{label}</span>
          {summary ? (
            <span className="text-muted-foreground text-xs">
              {`${t('invoices', { count: summary.invoiceCount })} · ${t('parcels', {
                count: summary.parcelCount,
              })}`}
            </span>
          ) : null}
        </p>

        {summary ? (
          <dl className="text-muted-foreground flex flex-wrap gap-x-4 gap-y-0.5 text-xs">
            {([['total', summary.totalDue], ...payments] as const).map(([key, value]) => (
              <div key={key} className="flex gap-1">
                <dt>{t(key)}</dt>
                <dd className="text-foreground font-semibold tabular-nums">{money(value)}</dd>
              </div>
            ))}
          </dl>
        ) : null}
      </div>

      <div className="flex flex-wrap gap-2">
        <PrintButton
          kind="document"
          subject={`Factures-${draft ? 'brouillons' : day}`}
          label={t('print')}
          onPrint={() => downloadInvoiceDayPdfAction(kind, { ...dayQuery(), day })}
        />
        <Button
          type="button"
          size="sm"
          variant="outline"
          className="gap-1.5"
          onClick={exportDay}
          disabled={exporting}
        >
          {exporting ? (
            <Loader2 className="size-4 animate-spin" />
          ) : (
            <Download className="size-4" aria-hidden />
          )}
          <span className="hidden sm:inline">{t('export')}</span>
        </Button>
        {canWrite && !draft && summary ? (
          <Button
            type="button"
            size="sm"
            variant="outline"
            className="gap-1.5"
            disabled={transferable.length === 0}
            title={transferable.length === 0 ? t('transferDone') : undefined}
            onClick={() => setTransferring(true)}
          >
            <Landmark className="size-4" aria-hidden />
            <span className="hidden sm:inline">
              {transferable.length === 0 ? t('transferDone') : t('transfer')}
            </span>
          </Button>
        ) : null}
      </div>

      {transferring ? (
        <DayTransferDialog
          label={label}
          codes={transferable}
          onDone={() => setTransferring(false)}
        />
      ) : null}
    </div>
  );
}

/**
 * La confirmation de l'ordre de virement du jour : un OV est numéroté et
 * journalisé, on ne le génère pas d'un clic involontaire sur un en-tête.
 */
function DayTransferDialog({
  label,
  codes,
  onDone,
}: {
  label: string;
  codes: string[];
  onDone: () => void;
}) {
  const t = useTranslations('finance.invoices.days');
  const tCommon = useTranslations('common');
  const router = useRouter();
  const [pending, setPending] = useState(false);

  const submit = async () => {
    if (pending) return;
    setPending(true);
    const created = await createTransferOrderAction({ invoiceCodes: codes });

    if (!created.ok) {
      setPending(false);
      toast.error(created.error);
      return;
    }

    const file = await downloadTransferOrderAction(created.data.code, 'pdf');
    setPending(false);
    if (file.ok) {
      downloadBase64(file.data, `Ordre-Virement-${created.data.code}.pdf`, 'application/pdf');
    } else {
      toast.error(file.error);
    }
    toast.success(created.data.code);
    // L'en-tête relit ses factures « à virer » : celles-ci n'y sont plus.
    router.refresh();
    onDone();
  };

  return (
    <Dialog open onOpenChange={(next) => (next || pending ? null : onDone())}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t('transferTitle', { date: label })}</DialogTitle>
          <DialogDescription>{t('transferHint', { count: codes.length })}</DialogDescription>
        </DialogHeader>
        <p className="text-muted-foreground font-mono text-xs break-words">{codes.join(', ')}</p>
        <DialogFooter>
          <Button variant="outline" onClick={onDone} disabled={pending}>
            {tCommon('cancel')}
          </Button>
          <Button onClick={submit} disabled={pending} className="gap-1.5">
            {pending ? <Loader2 className="size-4 animate-spin" /> : null}
            {t('transferSubmit')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
