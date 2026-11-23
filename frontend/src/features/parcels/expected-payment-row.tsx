'use client';

import type { ExpectedPayment } from '@delivery/shared';
import { Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
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
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ExpectedPaymentField } from '@/features/parcels/expected-payment-field';
import { useRouter } from '@/i18n/navigation';
import { changeExpectedPaymentAction } from '@/lib/actions/parcels';

/**
 * LE PAIEMENT ATTENDU sur la fiche colis — recette SpeedyTrans 3, point 3.
 *
 * Lu par tous ceux qui voient la fiche (admin, vendeur). Le back-office le
 * BASCULE à tout moment — ramassé ou non —, avec un motif qui part au journal
 * des explications : le client final paie parfois autrement que prévu, et
 * c'est au comptoir de le constater.
 */
export function ExpectedPaymentRow({
  code,
  value,
  canChange,
}: {
  code: string;
  value: ExpectedPayment | null;
  canChange: boolean;
}) {
  const t = useTranslations('parcels.expectedPayment');
  const tCommon = useTranslations('common');
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [next, setNext] = useState<ExpectedPayment>(value === 'cash' ? 'cheque' : 'cash');
  const [reason, setReason] = useState('');
  const [pending, startTransition] = useTransition();

  const submit = () => {
    startTransition(async () => {
      const result = await changeExpectedPaymentAction(code, { expectedPayment: next, reason });

      if (!result.ok) {
        toast.error(result.error);
        return;
      }

      toast.success(t('changed'));
      setOpen(false);
      setReason('');
      router.refresh();
    });
  };

  return (
    <div className="flex items-center justify-between gap-3 text-sm">
      <span className="text-muted-foreground">{t('label')}</span>
      <span className="flex items-center gap-2">
        <span className="font-medium">{value ? t(value) : '—'}</span>
        {canChange ? (
          <Button variant="ghost" size="sm" className="h-7 px-2" onClick={() => setOpen(true)}>
            {t('change')}
          </Button>
        ) : null}
      </span>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t('changeTitle', { code })}</DialogTitle>
            <DialogDescription>{t('changeHint')}</DialogDescription>
          </DialogHeader>

          <ExpectedPaymentField value={next} onChange={setNext} />

          <div className="flex flex-col gap-2">
            <Label htmlFor="expected-payment-reason">{t('reason')}</Label>
            <Input
              id="expected-payment-reason"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              maxLength={255}
            />
          </div>

          <DialogFooter className="gap-2 sm:gap-2">
            <Button type="button" variant="ghost" onClick={() => setOpen(false)}>
              {tCommon('cancel')}
            </Button>
            <Button
              type="button"
              disabled={pending || reason.trim().length === 0 || next === value}
              onClick={submit}
              className="gap-1.5"
            >
              {pending ? <Loader2 className="size-4 animate-spin" aria-hidden /> : null}
              {tCommon('save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
