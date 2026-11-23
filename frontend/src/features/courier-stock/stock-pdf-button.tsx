'use client';

import { COURIER_STOCK_PDF_ASYNC_THRESHOLD, type LabelJob } from '@delivery/shared';
import { Download, FileText, Loader2, TriangleAlert } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useSearchParams } from 'next/navigation';
import { useEffect, useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import {
  courierStockPdfAction,
  courierStockPdfJobAction,
  courierStockPdfJobFileAction,
  createCourierStockPdfJobAction,
} from '@/lib/actions/courier-stock';
import { downloadBase64 } from '@/lib/download';

/** Trois secondes, comme les étiquettes (D3) : suivre sans assaillir l'API. */
const POLL_MS = 3_000;

/**
 * « Situation du Stock Livreur » en PDF — lot M, M3 (D-222).
 *
 * Il part avec les filtres de l'URL, comme l'export (D4) : ce qui est affiché
 * est ce qui s'imprime. Jusqu'à `COURIER_STOCK_PDF_ASYNC_THRESHOLD` colis, le
 * fichier revient tout de suite ; au-delà, il se prépare dans la file des
 * étiquettes, et le bouton dit où il en est puis propose « Télécharger ».
 */
export function StockPdfButton({
  courierId,
  rows,
  label,
}: {
  /** `null` : le PDF global. */
  courierId: string | null;
  /** Les colis que le PDF portera — ce qui décide du chemin. */
  rows: number;
  label: string;
}) {
  const t = useTranslations('courierStock.pdf');
  const searchParams = useSearchParams();
  const [pending, setPending] = useState(false);
  const [job, setJob] = useState<LabelJob | null>(null);

  const query = (): Record<string, string> => {
    const result: Record<string, string> = {};
    for (const [key, value] of searchParams.entries()) {
      if (key !== 'page' && key !== 'pageSize') result[key] = value;
    }
    return result;
  };

  const running = job !== null && (job.status === 'pending' || job.status === 'running');

  useEffect(() => {
    if (!running || !job) return;
    const timer = setInterval(() => {
      void courierStockPdfJobAction(job.id).then((result) => {
        if (result.ok) setJob(result.data);
      });
    }, POLL_MS);
    return () => clearInterval(timer);
  }, [job, running]);

  const filename = (suffix: string) =>
    `${courierId ? 'stock-livreur' : 'stock-livreurs'}-${suffix}.pdf`;

  const run = async () => {
    setPending(true);

    if (rows > COURIER_STOCK_PDF_ASYNC_THRESHOLD) {
      const filters = query();
      const created = await createCourierStockPdfJobAction(
        courierId ? { kind: 'courier', courierId, ...filters } : { kind: 'global', ...filters },
      );
      setPending(false);
      if (!created.ok) {
        toast.error(created.error);
        return;
      }
      setJob(created.data);
      return;
    }

    const result = await courierStockPdfAction(courierId, query());
    setPending(false);
    if (!result.ok) {
      toast.error(result.error);
      return;
    }
    downloadBase64(result.data, filename(new Date().toISOString().slice(0, 10)), 'application/pdf');
  };

  const download = async () => {
    if (!job) return;
    setPending(true);
    const result = await courierStockPdfJobFileAction(job.id);
    setPending(false);
    if (!result.ok) {
      toast.error(result.error);
      return;
    }
    downloadBase64(result.data, filename(String(job.count)), 'application/pdf');
  };

  if (job?.status === 'ready') {
    return (
      <Button type="button" size="sm" className="gap-1.5" disabled={pending} onClick={download}>
        {pending ? (
          <Loader2 className="size-4 animate-spin" aria-hidden />
        ) : (
          <Download className="size-4" aria-hidden />
        )}
        {t('download')}
      </Button>
    );
  }

  if (running) {
    return (
      <span className="text-muted-foreground inline-flex items-center gap-1.5 text-sm">
        <Loader2 className="size-4 animate-spin" aria-hidden />
        {t('preparing', { count: String(job.count) })}
      </span>
    );
  }

  return (
    <>
      {job?.status === 'failed' ? (
        <span className="text-destructive inline-flex items-center gap-1.5 text-xs">
          <TriangleAlert className="size-4" aria-hidden />
          {t('failed')}
        </span>
      ) : null}
      <Button
        type="button"
        variant="outline"
        size="sm"
        className="gap-1.5"
        disabled={pending}
        onClick={run}
      >
        {pending ? (
          <Loader2 className="size-4 animate-spin" aria-hidden />
        ) : (
          <FileText className="size-4" aria-hidden />
        )}
        <span className="hidden sm:inline">{label}</span>
        <span className="sm:hidden">{t('button')}</span>
      </Button>
    </>
  );
}
