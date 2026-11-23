'use client';

import type { CourierBalance } from '@delivery/shared';
import { HandCoins, Loader2 } from 'lucide-react';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { settleCourierBalanceAction } from '@/lib/actions/finance';

const METHODS = ['cash', 'transfer', 'check'] as const;

/**
 * LE SOLDE D'UN LIVREUR — chantier « Finance », point 4.
 *
 * **Négatif veut dire « il doit ».** C'est le sens du grand livre, et l'écran
 * ne l'inverse pas pour faire joli : un agent qui lit « −200 » sur une fiche et
 * « −200 » dans le journal n'a rien à convertir dans sa tête.
 *
 * **Le règlement est une écriture de plus, jamais une remise à zéro.** Le solde
 * baisse parce qu'une ligne positive s'ajoute — et l'historique garde donc la
 * trace de ce qui a soldé quoi, et de qui l'a encaissé. Un bouton « solder »
 * aurait effacé cette question.
 *
 * **Rien n'est modifiable.** Une erreur se corrige par une écriture inverse,
 * comme dans n'importe quel journal comptable.
 */
export function CourierBalancePanel({
  balance,
  canSettle,
}: {
  balance: CourierBalance;
  /** `finance.pay` : c'est la même clé que l'encaissement d'un règlement client. */
  canSettle: boolean;
}) {
  const t = useTranslations('finance.balance');
  const tMethod = useTranslations('finance.methods');
  const tCommon = useTranslations('common');
  const format = useFormatter();

  const [open, setOpen] = useState(false);
  const [amount, setAmount] = useState('');
  const [method, setMethod] = useState<(typeof METHODS)[number]>('cash');
  const [note, setNote] = useState('');
  const [pending, startTransition] = useTransition();

  const money = (cents: number) => format.number(cents / 100, 'currency');

  /*
   * Le montant PROPOSÉ est ce qui reste dû, arrondi au dirham supérieur — pas
   * pour être malin, mais parce que c'est ce qu'on tape neuf fois sur dix. Il
   * reste modifiable : un livreur règle souvent en plusieurs fois.
   */
  const owed = balance.balance < 0 ? -balance.balance : 0;
  const cents = Math.round(Number(amount) * 100);

  const settle = () => {
    startTransition(async () => {
      const result = await settleCourierBalanceAction(balance.courierId, {
        amount: cents,
        method,
        note: note.trim(),
      });

      if (result.ok) {
        toast.success(t('settled'));
        setOpen(false);
        setAmount('');
        setNote('');
        return;
      }

      toast.error(result.error);
    });
  };

  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('title')}</CardTitle>
          <CardDescription>{t('hint')}</CardDescription>
        </CardHeader>

        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <p
              className={`text-3xl font-semibold tabular-nums ${
                balance.balance < 0 ? 'text-destructive' : ''
              }`}
            >
              {money(balance.balance)}
            </p>

            {canSettle && owed > 0 ? (
              <Button
                size="sm"
                className="gap-1.5"
                onClick={() => {
                  setAmount(String(owed / 100));
                  setOpen(true);
                }}
              >
                <HandCoins className="size-4" />
                {t('settle')}
              </Button>
            ) : null}
          </div>

          {balance.entries.length === 0 ? (
            <p className="text-muted-foreground text-sm">{t('empty')}</p>
          ) : (
            <ol className="flex flex-col gap-2">
              {balance.entries.map((entry) => (
                <li
                  key={entry.id}
                  className="flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3"
                >
                  <div className="min-w-0">
                    <span className="block text-sm font-medium">
                      {entry.kind === 'variance'
                        ? t('kinds.variance', { code: entry.remittanceCode ?? '—' })
                        : t('kinds.settlement', {
                            method: entry.method ? tMethod(entry.method) : '',
                          })}
                    </span>
                    <span className="text-muted-foreground block truncate text-xs">
                      {format.dateTime(new Date(entry.at), 'stamp')} · {entry.authorName}
                      {entry.note ? ` · ${entry.note}` : ''}
                    </span>
                  </div>

                  <span
                    className={`text-sm font-medium tabular-nums ${
                      entry.amount < 0 ? 'text-destructive' : ''
                    }`}
                  >
                    {money(entry.amount)}
                  </span>
                </li>
              ))}
            </ol>
          )}
        </CardContent>
      </Card>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('settle')}</DialogTitle>
            <DialogDescription>{t('settleHint', { amount: money(owed) })}</DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-3">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="settlement-amount">{t('amount')}</Label>
              <Input
                id="settlement-amount"
                type="number"
                min={0}
                step="0.01"
                value={amount}
                onChange={(event) => setAmount(event.target.value)}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="settlement-method">{t('method')}</Label>
              <Select value={method} onValueChange={(value) => setMethod(value as typeof method)}>
                <SelectTrigger id="settlement-method">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {METHODS.map((value) => (
                    <SelectItem key={value} value={value}>
                      {tMethod(value)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="settlement-note">{t('note')}</Label>
              <Textarea
                id="settlement-note"
                rows={2}
                value={note}
                onChange={(event) => setNote(event.target.value)}
              />
            </div>
          </div>

          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="outline" onClick={() => setOpen(false)} disabled={pending}>
              {tCommon('cancel')}
            </Button>
            <Button onClick={settle} disabled={pending || cents <= 0} className="gap-1.5">
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('settle')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
