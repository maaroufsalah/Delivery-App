'use client';

import type { PettyCashMovement } from '@delivery/shared';
import { Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
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
import { Textarea } from '@/components/ui/textarea';
import { refusePettyCashMovementAction } from '@/lib/actions/petty-cash';

/**
 * Refuser le brouillon d'un livreur — D-168. Le motif est facultatif et rendu
 * au livreur sur sa liste ; la décision est journalisée côté API.
 */
export function PettyCashRefuseDialog({
  movement,
  onDone,
}: {
  movement: PettyCashMovement;
  onDone: () => void;
}) {
  const t = useTranslations('finance.pettyCash');
  const tCommon = useTranslations('common');
  const tError = useTranslations('validation');
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);

  const refuse = async () => {
    if (saving) return;
    setSaving(true);
    const result = await refusePettyCashMovementAction(movement.id, { reason: reason.trim() });
    setSaving(false);

    if (result.ok) {
      toast.success(t('refusedToast'));
      onDone();
      return;
    }

    toast.error(
      result.error === 'invalid'
        ? tError('invalid')
        : result.reason === 'petty-cash-day-closed'
          ? t('days.closedError')
          : result.error,
    );
  };

  return (
    <Dialog open onOpenChange={(next) => (next ? null : onDone())}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t('refuseDialog.title')}</DialogTitle>
          <DialogDescription>
            {t('refuseDialog.hint', {
              label: movement.label ?? '',
              buyer: movement.buyerName ?? '',
            })}
          </DialogDescription>
        </DialogHeader>

        <Textarea
          rows={3}
          maxLength={300}
          placeholder={t('refuseDialog.reason')}
          value={reason}
          onChange={(event) => setReason(event.target.value)}
        />

        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="outline" onClick={onDone} disabled={saving}>
            {tCommon('cancel')}
          </Button>
          <Button variant="destructive" onClick={refuse} disabled={saving} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : null}
            {t('refuse')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
