'use client';

import type { ReturnSlipBulkSummary } from '@delivery/shared';
import { Layers, Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useEffect, useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  createHandoversInBulkAction,
  createSlipsInBulkAction,
  handoverBulkPreviewAction,
  slipBulkPreviewAction,
} from '@/lib/actions/returns';

/**
 * Le TYPE de bon est le seul paramètre — lot F, F3 (D-207) : les `BRT-` (bons
 * rendus au vendeur, lot E) et les `RL-` (bons de retour livreur). Mêmes deux
 * temps, mêmes mots, chacun dans son espace de traduction.
 */
const KINDS = {
  slips: {
    namespace: 'returns.slips.bulk',
    preview: slipBulkPreviewAction,
    create: createSlipsInBulkAction,
  },
  handovers: {
    namespace: 'returns.pending.bulk',
    preview: handoverBulkPreviewAction,
    create: createHandoversInBulkAction,
  },
} as const;

/**
 * LA CRÉATION EN MASSE DES BONS DE RETOUR — lot E, E2 (D-202), lot J (D-213).
 *
 * Deux temps, et c'est le serveur qui compte les deux fois : l'APERÇU annonce
 * « N bons pour N clients, M colis » sans rien écrire ; la confirmation crée,
 * en une transaction, et rend ce qu'elle a fait (« 3 bons créés, 27 colis »).
 * Toujours des bons NEUFS : un brouillon déjà ouvert n'est jamais complété. L'écran ne recalcule rien : entre l'aperçu et le clic, un agent
 * a pu remettre un bon ailleurs, et c'est le résultat qui fait foi.
 */
export function SlipBulkDialog({
  onDone,
  kind = 'slips',
}: {
  onDone: () => void;
  kind?: keyof typeof KINDS;
}) {
  const actions = KINDS[kind];
  const t = useTranslations(actions.namespace);
  const tCommon = useTranslations('common');

  const [preview, setPreview] = useState<ReturnSlipBulkSummary | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let alive = true;
    void actions.preview().then((result) => {
      if (!alive) return;
      if (result.ok) setPreview(result.data);
      else {
        toast.error(result.error);
        onDone();
      }
    });
    return () => {
      alive = false;
    };
  }, [actions, onDone]);

  const confirm = async () => {
    setSaving(true);
    const result = await actions.create();
    setSaving(false);

    if (!result.ok) {
      toast.error(result.error);
      return;
    }

    const done = result.data;
    toast.success(
      done.parcels === 0
        ? t('doneNothing')
        : t('done', { created: done.created, parcels: done.parcels }),
    );
    onDone();
  };

  return (
    <Dialog open onOpenChange={(next) => (next || saving ? null : onDone())}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{t('title')}</DialogTitle>
          <DialogDescription>{t('hint')}</DialogDescription>
        </DialogHeader>

        {preview === null ? (
          <p className="text-muted-foreground flex items-center gap-2 text-sm">
            <Loader2 className="size-4 animate-spin" aria-hidden />
            {t('loading')}
          </p>
        ) : preview.parcels === 0 ? (
          <p className="text-muted-foreground text-sm">{t('nothing')}</p>
        ) : (
          <p className="text-base font-semibold">
            {t('summary', {
              slips: preview.created,
              clients: preview.clients,
              parcels: preview.parcels,
            })}
          </p>
        )}

        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="outline" onClick={onDone} disabled={saving}>
            {tCommon('cancel')}
          </Button>
          <Button
            onClick={() => void confirm()}
            disabled={saving || preview === null || preview.parcels === 0}
            className="gap-1.5"
          >
            {saving ? (
              <Loader2 className="size-4 animate-spin" aria-hidden />
            ) : (
              <Layers className="size-4" aria-hidden />
            )}
            {t('confirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
