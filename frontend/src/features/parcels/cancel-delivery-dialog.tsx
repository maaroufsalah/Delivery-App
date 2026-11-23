'use client';

import { findParcelStatus } from '@delivery/shared';
import { Loader2, Undo2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useRouter } from '@/i18n/navigation';
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
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { cancelDeliveryAction } from '@/lib/actions/parcels';

/** Les refus que l'API motive (D-210), traduits ; un autre garde le message serveur. */
const REASONS = [
  'parcel-remitted',
  'parcel-invoiced',
  'parcel-paid-out',
  'parcel-on-statement',
  'cheque-handed-over',
  'bl-already-received',
  'exchange-delivered',
  'parcel-not-delivered',
  'parcel-status-changed',
  'cancel-delivery-admin-only',
] as const;

type CancelErrorKey = (typeof REASONS)[number];

/**
 * « ANNULER LA LIVRAISON » — lot I, I2 (D-210).
 *
 * Un « Livré » posé par erreur, défait par l'administrateur (la page ne rend
 * ce bouton qu'à un SUPER_ADMIN, et l'API le refuse à tout autre). Motif
 * obligatoire, sur le modèle du retour arrière d'un BL (D-204). Le serveur
 * rend le colis à sa tournée, annule le COD et la commission, recalcule le
 * retour de fonds ouvert du livreur — ou refuse si l'argent est déjà remis ou
 * facturé.
 */
export function CancelDeliveryDialog({ code }: { code: string }) {
  const t = useTranslations('parcels.detail.cancelDelivery');
  const tStatus = useTranslations('status');
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);

  const submit = async () => {
    setSaving(true);
    const result = await cancelDeliveryAction(code, { reason });
    setSaving(false);

    if (result.ok) {
      const status = findParcelStatus(result.data.statusId);
      toast.success(
        t(result.data.routeReopened ? 'doneReopened' : 'done', {
          status: status ? tStatus(status.key) : String(result.data.statusId),
          route: result.data.routeCode ?? '',
        }),
      );
      setOpen(false);
      setReason('');
      router.refresh();
      return;
    }

    const known = result.reason && (REASONS as readonly string[]).includes(result.reason);
    toast.error(
      known
        ? t(`errors.${result.reason as CancelErrorKey}`)
        : result.error === 'invalid'
          ? t('reasonHint')
          : result.error,
    );
  };

  return (
    <>
      <Button variant="outline" size="sm" className="gap-1.5" onClick={() => setOpen(true)}>
        <Undo2 className="flip-rtl size-4" aria-hidden />
        <span>{t('action')}</span>
      </Button>

      <Dialog open={open} onOpenChange={(next) => (saving ? undefined : setOpen(next))}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t('title')}</DialogTitle>
            <DialogDescription>{t('hint')}</DialogDescription>
          </DialogHeader>

          <p className="bg-muted/60 rounded-md px-3 py-2 font-mono text-xs">{code}</p>

          <div className="flex flex-col gap-2">
            <Label htmlFor="cancel-delivery-reason">{t('reason')}</Label>
            <Textarea
              id="cancel-delivery-reason"
              rows={3}
              maxLength={255}
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
            <p className="text-muted-foreground text-xs">{t('reasonHint')}</p>
          </div>

          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="outline" onClick={() => setOpen(false)} disabled={saving}>
              {t('cancel')}
            </Button>
            <Button
              variant="destructive"
              onClick={submit}
              disabled={saving || reason.trim().length === 0}
              className="gap-1.5"
            >
              {saving ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('confirm')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
