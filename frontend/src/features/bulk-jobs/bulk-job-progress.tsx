'use client';

import type { BulkJob } from '@delivery/shared';
import { AlertTriangle, CheckCircle2, Loader2, RotateCcw } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useEffect, useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useRouter } from '@/i18n/navigation';
import { bulkJobAction, retryBulkJobAction } from '@/lib/actions/bulk-jobs';

/** Le rythme de relecture d'une tâche en cours — celui des étiquettes (D-190) est de 3 s. */
const POLL_MS = 2_000;

/** Combien d'échecs on nomme à l'écran ; le reste se compte. */
const FAILURES_SHOWN = 50;

/** Au-delà, une tâche terminée sans échec ne s'affiche plus. */
const STALE_MS = 30 * 60_000;

const active = (job: BulkJob | null) => job?.status === 'pending' || job?.status === 'running';

/**
 * LA PROGRESSION D'UNE TÂCHE DE MASSE — lot W2 (D-239), audit E2E-001.
 *
 * « 1 200 / 5 000 », relue toutes les 2 s tant que la tâche travaille. La page
 * la reçoit de son composant serveur (la dernière tâche de l'objet) : elle se
 * retrouve après un rechargement. Terminée, la page se relit ; arrêtée, elle
 * dit pourquoi et se relance là où elle en était ; les colis non traités sont
 * nommés, avec leur motif.
 */
export function BulkJobProgress({ job: initial }: { job: BulkJob | null }) {
  const t = useTranslations('bulkJobs');
  const tError = useTranslations('routes.errors');
  const router = useRouter();
  const [job, setJob] = useState<BulkJob | null>(initial);
  const [retrying, setRetrying] = useState(false);
  // L'heure d'ouverture de la page, une fois : le rendu reste pur.
  const [openedAt] = useState(() => Date.now());

  // Une nouvelle tâche relue par la page (rechargement, geste relancé) remplace l'ancienne.
  const [received, setReceived] = useState(initial);
  if (initial !== received) {
    setReceived(initial);
    setJob(initial);
  }

  useEffect(() => {
    if (!active(job) || !job) return;
    const id = job.id;
    const timer = setInterval(async () => {
      const result = await bulkJobAction(id);
      if (!result.ok) return;
      setJob(result.data);
      if (result.data.status === 'done') {
        toast.success(t('doneToast', { count: String(result.data.total) }));
        router.refresh();
      }
      if (result.data.status === 'failed') toast.error(t('failedToast'));
    }, POLL_MS);
    return () => clearInterval(timer);
  }, [job, router, t]);

  // Une tâche terminée sans échec depuis plus de 30 min n'a plus rien à dire.
  const stale =
    job?.status === 'done' &&
    job.failures.length === 0 &&
    job.finishedAt !== null &&
    openedAt - Date.parse(job.finishedAt) > STALE_MS;
  if (!job || stale) return null;

  const percent = job.total > 0 ? Math.round((job.processed / job.total) * 100) : 0;
  const reason = (code: string) => (tError.has(code as never) ? tError(code as never) : code);

  const retry = async () => {
    setRetrying(true);
    const result = await retryBulkJobAction(job.id);
    setRetrying(false);
    if (result.ok) setJob(result.data);
    else toast.error(result.error);
  };

  return (
    <Card aria-live="polite">
      <CardHeader className="pb-2">
        <CardTitle className="flex items-center gap-2 text-base">
          {active(job) ? (
            <Loader2 className="size-4 animate-spin" aria-hidden />
          ) : job.status === 'done' ? (
            <CheckCircle2 className="size-4 text-emerald-600" aria-hidden />
          ) : (
            <AlertTriangle className="text-destructive size-4" aria-hidden />
          )}
          {t(`kind.${job.kind}`)} — {t(`status.${job.status}`)}
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <div className="flex flex-col gap-1">
          <div className="bg-muted h-2 w-full overflow-hidden rounded-full">
            <div
              className="bg-primary h-full rounded-full transition-[width]"
              style={{ width: `${percent}%` }}
              role="progressbar"
              aria-valuemin={0}
              aria-valuemax={job.total}
              aria-valuenow={job.processed}
            />
          </div>
          <p className="text-muted-foreground text-sm tabular-nums">
            {t('progress', { processed: String(job.processed), total: String(job.total) })}
          </p>
        </div>

        {job.status === 'failed' ? (
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="text-destructive text-sm">
              {t('stopped', { reason: job.error ? reason(job.error) : '—' })}
            </p>
            <Button size="sm" variant="outline" disabled={retrying} onClick={retry}>
              <RotateCcw className="size-4" aria-hidden />
              {t('retry')}
            </Button>
          </div>
        ) : null}

        {job.failures.length > 0 ? (
          <div className="flex flex-col gap-1">
            <p className="text-sm font-medium">{t('failuresTitle', { count: job.failures.length })}</p>
            <ul className="text-muted-foreground max-h-48 overflow-y-auto text-xs">
              {job.failures.slice(0, FAILURES_SHOWN).map((failure) => (
                <li key={failure.parcelId} className="flex gap-2">
                  <span className="font-mono">{failure.code ?? failure.parcelId}</span>
                  <span>{reason(failure.reason)}</span>
                </li>
              ))}
            </ul>
            {job.failures.length > FAILURES_SHOWN ? (
              <p className="text-muted-foreground text-xs">
                {t('failuresMore', { count: String(job.failures.length - FAILURES_SHOWN) })}
              </p>
            ) : null}
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}
