'use client';

import type { InvoiceDetail as Invoice } from '@delivery/shared';
import { Loader2, Plus, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Textarea } from '@/components/ui/textarea';
import { useFinanceError } from '@/features/finance/use-finance-error';
import { addInvoiceAdjustmentAction, removeInvoiceAdjustmentAction } from '@/lib/actions/finance';

/**
 * LES LIGNES HORS COLIS D'UNE FACTURE — lot Argent 2, A2-1 (D-181).
 *
 * Une régularisation, un frais de ramassage par bon (posé par la validation du
 * bon, A2-5), un autre ajustement : un montant HT signé et son motif, sous les
 * colis — comme sur le PDF. « Ajouter une ligne » et la suppression n'existent
 * que sur un BROUILLON et pour l'administration (`finance.validate`) ; le
 * serveur refuse le reste quoi qu'envoie l'écran.
 */
export function InvoiceAdjustmentsCard({
  invoice,
  canAdjust,
  onChange,
  internalView = true,
}: {
  invoice: Invoice;
  /** `finance.validate` : l'administration seule pose un ajustement. */
  canAdjust: boolean;
  onChange: (invoice: Invoice) => void;
  /** Lot V, V2 : faux pour le vendeur, qui lit l'aide à la deuxième personne, sans « OV ». */
  internalView?: boolean;
}) {
  const t = useTranslations('finance.invoices.adjustments');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const errorMessage = useFinanceError();
  const [pending, startTransition] = useTransition();
  const [open, setOpen] = useState(false);
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');
  const [reference, setReference] = useState('');

  const adjustments = invoice.adjustments ?? [];
  const editable = canAdjust && invoice.adjustable;
  const money = (cents: number) => format.number(cents / 100, 'currency');

  // Rien à montrer, rien à faire : la carte ne s'affiche pas.
  if (adjustments.length === 0 && !editable) return null;

  const parsedAmount = Number(amount.replace(',', '.'));
  const valid = Number.isFinite(parsedAmount) && parsedAmount !== 0 && reason.trim().length > 0;

  const submit = () => {
    startTransition(async () => {
      const result = await addInvoiceAdjustmentAction(invoice.code, {
        kind: 'adjustment',
        amount: parsedAmount,
        reason: reason.trim(),
        reference: reference.trim() || undefined,
      });
      if (result.ok) {
        onChange(result.data);
        setOpen(false);
        setAmount('');
        setReason('');
        setReference('');
        toast.success(t('added'));
      } else toast.error(errorMessage(result));
    });
  };

  const remove = (id: string) => {
    startTransition(async () => {
      const result = await removeInvoiceAdjustmentAction(invoice.code, id);
      if (result.ok) {
        onChange(result.data);
        toast.success(t('removed'));
      } else toast.error(errorMessage(result));
    });
  };

  return (
    <Card>
      <CardHeader className="flex flex-row flex-wrap items-start gap-2">
        <div className="min-w-0 flex-1">
          <CardTitle>{t('title')}</CardTitle>
          <CardDescription>{internalView ? t('hint') : t('hintMerchant')}</CardDescription>
        </div>
        {editable ? (
          <Button size="sm" variant="outline" className="gap-1.5" onClick={() => setOpen(true)}>
            <Plus className="size-4" />
            {t('add')}
          </Button>
        ) : null}
      </CardHeader>
      <CardContent>
        {adjustments.length === 0 ? (
          <p className="text-muted-foreground py-4 text-center text-sm">{t('empty')}</p>
        ) : (
          <ul className="divide-border divide-y">
            {adjustments.map((line) => (
              <li key={line.id} className="flex items-center gap-3 py-2.5">
                <span className="min-w-0 flex-1">
                  <span className="block text-sm">{line.reason}</span>
                  <span className="text-muted-foreground block truncate text-xs">
                    {t(`kind.${line.kind}`)}
                    {line.reference ? ` · ${line.reference}` : ''}
                    {line.createdByName ? ` · ${line.createdByName}` : ''}
                  </span>
                </span>
                <span className="shrink-0 text-sm font-medium tabular-nums">
                  {money(line.amount)}
                </span>
                {editable ? (
                  <Button
                    size="icon"
                    variant="ghost"
                    className="text-destructive shrink-0"
                    disabled={pending}
                    aria-label={tCommon('delete')}
                    onClick={() => remove(line.id)}
                  >
                    <Trash2 className="size-4" />
                  </Button>
                ) : null}
              </li>
            ))}
          </ul>
        )}
      </CardContent>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('add')}</DialogTitle>
            <DialogDescription>{t('dialogHint')}</DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="adjustment-amount">{t('fields.amount')}</Label>
              <Input
                id="adjustment-amount"
                inputMode="decimal"
                value={amount}
                placeholder="1180"
                onChange={(event) => setAmount(event.target.value)}
              />
              <p className="text-muted-foreground text-xs">{t('fields.amountHint')}</p>
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="adjustment-reason">{t('fields.reason')}</Label>
              <Textarea
                id="adjustment-reason"
                rows={2}
                value={reason}
                onChange={(event) => setReason(event.target.value)}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="adjustment-reference">{t('fields.reference')}</Label>
              <Input
                id="adjustment-reference"
                value={reference}
                placeholder="FA-2610-0004"
                onChange={(event) => setReference(event.target.value)}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              {tCommon('cancel')}
            </Button>
            <Button disabled={pending || !valid} onClick={submit} className="gap-1.5">
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('confirm')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
