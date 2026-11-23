'use client';

import type { LabelJob } from '@delivery/shared';
import { Download, FileText, Loader2, TriangleAlert } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useCallback, useEffect, useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { labelJobFileAction, labelJobsAction } from '@/lib/actions/documents';
import { downloadBase64 } from '@/lib/download';

/** Trois secondes : assez pour suivre, sans assaillir l'API pendant une minute de rendu. */
const POLL_MS = 3_000;

/**
 * LES ÉTIQUETTES PRÉPARÉES EN ARRIÈRE-PLAN — lot D, D3 (D-190).
 *
 * Les travaux du compte (les siens seulement, 24 h), relus à l'ouverture de la
 * page : on peut lancer 2 000 étiquettes, aller ailleurs, revenir et
 * télécharger. Tant qu'un travail est en cours, la liste se relit toutes les
 * trois secondes ; elle s'arrête dès qu'il n'y en a plus.
 */
export function useLabelJobs(): { jobs: LabelJob[]; add: (job: LabelJob) => void } {
  const [jobs, setJobs] = useState<LabelJob[]>([]);

  useEffect(() => {
    let alive = true;
    void labelJobsAction().then((result) => {
      if (alive && result.ok) setJobs(result.data);
    });
    return () => {
      alive = false;
    };
  }, []);

  const active = jobs.some((job) => job.status === 'pending' || job.status === 'running');

  useEffect(() => {
    if (!active) return;
    const timer = setInterval(() => {
      void labelJobsAction().then((result) => {
        if (result.ok) setJobs(result.data);
      });
    }, POLL_MS);
    return () => clearInterval(timer);
  }, [active]);

  const add = useCallback((job: LabelJob) => {
    setJobs((current) => [job, ...current.filter((entry) => entry.id !== job.id)]);
  }, []);

  return { jobs, add };
}

export function LabelJobsPanel({ jobs }: { jobs: LabelJob[] }) {
  const t = useTranslations('parcels.labelJobs');
  const format = useFormatter();
  const [downloading, setDownloading] = useState<string | null>(null);

  if (jobs.length === 0) return null;

  const download = async (job: LabelJob) => {
    setDownloading(job.id);
    const result = await labelJobFileAction(job.id);
    setDownloading(null);

    if (!result.ok) {
      toast.error(result.error);
      return;
    }
    // Lot W2 (D-239) : au-delà de 1 000 étiquettes, une archive de PDF.
    const zip = job.fileType === 'zip';
    downloadBase64(
      result.data,
      `etiquettes-${job.count}.${zip ? 'zip' : 'pdf'}`,
      zip ? 'application/zip' : 'application/pdf',
    );
  };

  return (
    <section aria-label={t('title')} className="mb-4 flex flex-col gap-2">
      {jobs.map((job) => {
        const count = String(job.count);

        return (
          <div
            key={job.id}
            className="bg-card flex flex-wrap items-center gap-3 rounded-lg border px-3 py-2 text-sm"
          >
            {job.status === 'ready' ? (
              <FileText className="text-muted-foreground size-4 shrink-0" aria-hidden />
            ) : job.status === 'failed' ? (
              <TriangleAlert className="text-destructive size-4 shrink-0" aria-hidden />
            ) : (
              <Loader2 className="text-muted-foreground size-4 shrink-0 animate-spin" aria-hidden />
            )}

            <span className="min-w-0 flex-1">
              {job.status === 'ready' ? (
                <>
                  <span className="block font-medium">{t('ready', { count })}</span>
                  <span className="text-muted-foreground block text-xs">
                    {t('readyUntil', {
                      time: format.dateTime(new Date(job.expiresAt), 'stamp'),
                    })}
                  </span>
                </>
              ) : job.status === 'failed' ? (
                <span className="text-destructive">
                  {job.error === 'interrupted'
                    ? t('interrupted', { count })
                    : t('failed', { count })}
                </span>
              ) : (
                <span>{t('preparing', { count })}</span>
              )}
            </span>

            {job.status === 'ready' ? (
              <Button
                type="button"
                size="sm"
                className="gap-1.5"
                disabled={downloading === job.id}
                onClick={() => void download(job)}
              >
                {downloading === job.id ? (
                  <Loader2 className="size-4 animate-spin" aria-hidden />
                ) : (
                  <Download className="size-4" aria-hidden />
                )}
                {t('download')}
              </Button>
            ) : null}
          </div>
        );
      })}
    </section>
  );
}
