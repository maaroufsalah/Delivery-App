'use client';

import { DELIVERED, findParcelStatus } from '@delivery/shared';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { reportOutcomeAction } from '@/lib/actions/courier';
import { cn } from '@/lib/utils';

/**
 * RAPPORTER UNE ISSUE, depuis le web — L1 bloc 3.
 *
 * **Les issues sont celles que le serveur autorise**, passées en propriété par
 * la page : la machine à états vit dans `packages/shared`, le filtre de rôle
 * dans l'API, et les recopier ici ferait une troisième table à tenir d'accord.
 *
 * **Le montant encaissé est le dû, en lecture seule** — dernières
 * corrections, point 4. Seul le chèque se saisit ; l'API refuse en 422 tout
 * autre montant venu d'un livreur, et la correction d'un encaissement reste un
 * geste d'agence. Le champ n'apparaît que pour « livré ».
 *
 * **Pas de position GPS ici**, contrairement au mobile : un poste d'agence est
 * à l'agence, et l'y enregistrer prouverait seulement que la livraison a été
 * saisie au bureau. Le mobile, lui, est devant la porte.
 */
export function OutcomeDialog({
  code,
  outcomes,
  dueInCents,
}: {
  code: string;
  outcomes: number[];
  dueInCents: number;
}) {
  const t = useTranslations('courier.parcel');
  const tStatus = useTranslations('status');
  const tCommon = useTranslations('common');

  const [open, setOpen] = useState(false);
  const [chosen, setChosen] = useState<number | null>(null);
  const [chequeAmount, setChequeAmount] = useState('');
  const [chequeNumber, setChequeNumber] = useState('');
  const [chequeBank, setChequeBank] = useState('');
  const [scheduledAt, setScheduledAt] = useState('');
  const [comment, setComment] = useState('');
  const [pending, startTransition] = useTransition();

  const delivered = chosen === DELIVERED;
  // 14 : « reporté ». Le référentiel ne l’exporte pas sous un nom — il n’y a
  // qu’ici que le web en a besoin, et l’inventer dans packages/shared pour un
  // seul appelant ferait une constante de plus à chercher.
  const postponed = chosen === 14;

  /*
   * LE PAIEMENT MIXTE — chantier « Finance », point 8.
   *
   * **Le livreur ne saisit QUE le chèque.** Les espèces sont la différence, et
   * elles s'affichent en temps réel : lui faire saisir les deux aurait invité
   * la faute d'addition au pas de porte, une main sur le téléphone et l'autre
   * sur un carton.
   *
   * Le contrôle « chèque ≤ total » est ici ET dans l'API : un écran n'est pas
   * un rempart, il se contourne avec un vieux cache.
   */
  const collectedCents = dueInCents;
  const chequeCents = Math.round(Number(chequeAmount || '0') * 100);
  const cashCents = collectedCents - chequeCents;
  const chequeTooLarge = chequeCents > collectedCents;

  const submit = () => {
    if (chosen === null) return;

    startTransition(async () => {
      const result = await reportOutcomeAction(code, {
        statusId: chosen,
        ...(comment.trim() ? { comment: comment.trim() } : {}),
        ...(delivered ? { collectedAmount: collectedCents / 100 } : {}),
        ...(delivered && chequeCents > 0
          ? {
              chequeAmount: Number(chequeAmount),
              ...(chequeNumber.trim() ? { chequeNumber: chequeNumber.trim() } : {}),
              ...(chequeBank.trim() ? { chequeBank: chequeBank.trim() } : {}),
            }
          : {}),
        ...(postponed && scheduledAt ? { scheduledAt } : {}),
      });

      if (!result.ok) {
        toast.error(result.error);
        return;
      }

      toast.success(t('reported'));
      setOpen(false);
      setChosen(null);
      setComment('');
    });
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="w-full sm:w-auto">{t('report')}</Button>
      </DialogTrigger>

      <DialogContent className="max-h-[90dvh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t('report')}</DialogTitle>
        </DialogHeader>

        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-2">
            {outcomes.map((statusId) => {
              const target = findParcelStatus(statusId);
              if (!target) return null;

              return (
                <button
                  key={statusId}
                  type="button"
                  aria-pressed={chosen === statusId}
                  onClick={() => setChosen(statusId)}
                  className={cn(
                    'flex min-h-12 items-center gap-3 rounded-xl border px-3 text-start text-sm',
                    chosen === statusId ? 'border-primary bg-muted' : 'hover:bg-muted/60',
                  )}
                >
                  <span
                    className="size-2.5 shrink-0 rounded-full"
                    style={{ backgroundColor: target.color }}
                  />
                  {tStatus(target.key)}
                </button>
              );
            })}
          </div>

          {delivered ? (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="collected">{t('collected')}</Label>
              <Input
                id="collected"
                value={(collectedCents / 100).toFixed(2)}
                readOnly
                className="bg-muted tabular-nums"
              />
            </div>
          ) : null}

          {delivered ? (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="cheque">{t('chequeAmount')}</Label>
              <Input
                id="cheque"
                inputMode="decimal"
                value={chequeAmount}
                placeholder={t('chequePlaceholder')}
                onChange={(event) => setChequeAmount(event.target.value)}
              />

              {/*
                LES ESPÈCES, EN DIRECT. C'est la ligne que le livreur lit avant
                de rendre la monnaie ; l'afficher après coup n'aurait servi à
                personne.
              */}
              <p
                className={`text-xs tabular-nums ${chequeTooLarge ? 'text-destructive' : 'text-muted-foreground'}`}
              >
                {chequeTooLarge
                  ? t('chequeTooLarge')
                  : t('cashComputed', { amount: (cashCents / 100).toFixed(2) })}
              </p>

              {chequeCents > 0 ? (
                <div className="grid gap-2 sm:grid-cols-2">
                  <Input
                    aria-label={t('chequeNumber')}
                    value={chequeNumber}
                    placeholder={t('chequeNumber')}
                    onChange={(event) => setChequeNumber(event.target.value)}
                  />
                  <Input
                    aria-label={t('chequeBank')}
                    value={chequeBank}
                    placeholder={t('chequeBank')}
                    onChange={(event) => setChequeBank(event.target.value)}
                  />
                </div>
              ) : null}
            </div>
          ) : null}

          {postponed ? (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="scheduled">{t('scheduledAt')}</Label>
              <Input
                id="scheduled"
                type="date"
                value={scheduledAt}
                onChange={(event) => setScheduledAt(event.target.value)}
              />
            </div>
          ) : null}

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="comment">{t('reason')}</Label>
            <Textarea
              id="comment"
              value={comment}
              onChange={(event) => setComment(event.target.value)}
              rows={3}
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="ghost" onClick={() => setOpen(false)} disabled={pending}>
            {tCommon('cancel')}
          </Button>
          <Button onClick={submit} disabled={chosen === null || pending || chequeTooLarge}>
            {t('report')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
