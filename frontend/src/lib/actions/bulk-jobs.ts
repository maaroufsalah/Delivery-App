'use server';

import type { BulkJob } from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import { getBulkJob, retryBulkJob } from '@/lib/api/bulk-jobs';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';

/**
 * Les tâches de masse — lot W2 (D-239). L'écran les suit par ces actions (D-018 :
 * aucun composant client ne parle à l'API en direct).
 */
export async function bulkJobAction(id: string): Promise<ReferentialResult<BulkJob>> {
  if (!id) return { ok: false, error: 'invalid' };
  try {
    const job = await getBulkJob(id);
    // Le dernier pas a changé l'objet (bon validé, circuit parti) : ses pages se relisent.
    if (job.status === 'done') {
      for (const path of [
        '/[locale]/(app)/routes/[code]',
        '/[locale]/(app)/routes',
        '/[locale]/(app)/pickup-notes/[code]',
        '/[locale]/(app)/dropoff-notes/[code]',
        '/[locale]/(app)/pickup-notes',
        '/[locale]/(app)/dropoff-notes',
        '/[locale]/(app)/parcels',
      ]) {
        revalidatePath(path, 'page');
      }
    }
    return { ok: true, data: job };
  } catch (error) {
    return toFailure(error);
  }
}

export async function retryBulkJobAction(id: string): Promise<ReferentialResult<BulkJob>> {
  if (!id) return { ok: false, error: 'invalid' };
  try {
    return { ok: true, data: await retryBulkJob(id) };
  } catch (error) {
    return toFailure(error);
  }
}
