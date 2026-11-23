'use client';

import type { ParcelDetail, ParcelFeeChange } from '@delivery/shared';
import { Lock, PencilLine } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
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
import { Textarea } from '@/components/ui/textarea';
import { editParcelFeesAction } from '@/lib/actions/parcels';

/** Obligatoire, sans longueur minimale (12/11) : comme le schéma, seul le vide est refusé. */
const MIN_REASON = 1;

/**
 * Modifier les MONTANTS d'un colis — C11 bloc 4 (D-063), élargi au chantier
 * « Finance », point 2.
 *
 * Le bouton n'apparaît que pour `parcels.editFees` ; il est GRISÉ, et non
 * caché, quand le colis est sur une facture RÉGLÉE. Le voir grisé avec le
 * numéro de la facture en dessous dit la règle ; son absence laisserait
 * chercher.
 *
 * **Le verrou s'est desserré, et c'est voulu.** C11 refusait toute
 * modification dès l'ÉMISSION. La prudence se défendait, mais les lignes de
 * facture sont des copies gelées (D-036) : corriger le colis ne touche pas la
 * pièce comptable, et une erreur découverte le lendemain n'avait plus aucun
 * recours. Ce qui reste verrouillé, c'est la facture PAYÉE — de l'argent a
 * changé de mains, et cela se corrige par un avoir.
 *
 * Quatre montants, un motif obligatoire. Le motif n'est pas une formalité :
 * c'est ce qui permet de défendre un montant devant un e-commerçant trois
 * semaines plus tard, et le legacy laissait `colis.frais` se réécrire sans
 * rien.
 */
export function ParcelFeesEditor({ parcel, canEdit }: { parcel: ParcelDetail; canEdit: boolean }) {
  const t = useTranslations('parcels.fees');
  const tCommon = useTranslations('common');
  const [open, setOpen] = useState(false);
  const [pending, startTransition] = useTransition();

  // En dirhams à la saisie, comme partout où l'utilisateur tape un montant ;
  // la conversion en centimes est faite au bord (`src/lib/http.ts` → API).
  const [codAmount, setCodAmount] = useState(String(parcel.codAmount / 100));
  const [deliveryFee, setDeliveryFee] = useState(String(parcel.deliveryFee / 100));
  const [failureFee, setFailureFee] = useState(
    parcel.failureFee === null ? '' : String(parcel.failureFee / 100),
  );
  const [otherFees, setOtherFees] = useState(String(parcel.otherFees / 100));
  const [reason, setReason] = useState('');

  // La facture RÉGLÉE, et elle seule — point 2. L'émission n'enferme plus rien.
  const locked = parcel.invoiceSettledOn !== null;
  const reasonTooShort = reason.trim().length < MIN_REASON;

  const submit = () => {
    startTransition(async () => {
      const result = await editParcelFeesAction(parcel.code, {
        codAmount: Number(codAmount),
        deliveryFee: Number(deliveryFee),
        ...(failureFee === '' ? {} : { failureFee: Number(failureFee) }),
        otherFees: Number(otherFees),
        reason: reason.trim(),
      });

      if (result.ok) {
        toast.success(t('saved'));
        setOpen(false);
        setReason('');
        return;
      }

      toast.error(
        result.reason === 'parcel-invoice-paid' || result.reason === 'parcel-already-invoiced'
          ? t('lockedToast')
          : result.error,
      );
    });
  };

  if (!canEdit) return null;

  return (
    <>
      <div className="flex flex-col gap-1.5">
        <Button
          type="button"
          size="sm"
          variant="outline"
          className="w-fit gap-1.5"
          disabled={locked}
          onClick={() => setOpen(true)}
        >
          {locked ? (
            <Lock className="size-4" aria-hidden />
          ) : (
            <PencilLine className="size-4" aria-hidden />
          )}
          {t('edit')}
        </Button>

        {/*
          Le NUMÉRO de la facture, pas « ce colis est facturé » : sans lui,
          l'agent n'a aucun moyen de retrouver laquelle depuis cet écran.
        */}
        {locked ? (
          <p className="text-muted-foreground text-xs">
            {t('lockedBy', { code: parcel.invoiceSettledOn ?? '' })}
          </p>
        ) : null}
      </div>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('edit')}</DialogTitle>
            <DialogDescription>{t('hint')}</DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-3">
            <div className="grid gap-3 sm:grid-cols-2">
              {/*
                LE PRIX DU COLIS en premier — point 2. C'est le montant que le
                destinataire voit, et celui qu'une erreur de saisie touche le
                plus souvent : il arrive du fichier d'import du vendeur.
              */}
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="codAmount">{t('codAmount')}</Label>
                <Input
                  id="codAmount"
                  type="number"
                  min={0}
                  step="0.01"
                  value={codAmount}
                  onChange={(event) => setCodAmount(event.target.value)}
                />
              </div>

              <div className="flex flex-col gap-1.5">
                <Label htmlFor="deliveryFee">{t('deliveryFee')}</Label>
                <Input
                  id="deliveryFee"
                  type="number"
                  min={0}
                  step="0.01"
                  value={deliveryFee}
                  onChange={(event) => setDeliveryFee(event.target.value)}
                />
              </div>

              <div className="flex flex-col gap-1.5">
                <Label htmlFor="failureFee">{t('failureFee')}</Label>
                <Input
                  id="failureFee"
                  type="number"
                  min={0}
                  step="0.01"
                  value={failureFee}
                  placeholder={t('failureFeePlaceholder')}
                  onChange={(event) => setFailureFee(event.target.value)}
                />
              </div>

              <div className="flex flex-col gap-1.5">
                <Label htmlFor="otherFees">{t('otherFees')}</Label>
                <Input
                  id="otherFees"
                  type="number"
                  min={0}
                  step="0.01"
                  value={otherFees}
                  onChange={(event) => setOtherFees(event.target.value)}
                />
              </div>
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="reason">{t('reason')}</Label>
              <Textarea
                id="reason"
                rows={3}
                value={reason}
                placeholder={t('reasonPlaceholder')}
                onChange={(event) => setReason(event.target.value)}
              />
              <p className="text-muted-foreground text-xs">{t('reasonHint')}</p>
            </div>

            {/*
              LA phrase du bloc. Un agent qui corrige un tarif se demande s'il
              vient de changer la paie du livreur : la réponse est non, et elle
              est écrite là plutôt que dans une note de version.
            */}
            <p className="text-muted-foreground border-border border-t pt-3 text-xs">
              {t('commissionUntouched')}
            </p>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              {tCommon('cancel')}
            </Button>
            <Button disabled={pending || reasonTooShort} onClick={submit}>
              {tCommon('save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

/**
 * L'historique des frais, sur la fiche — ancien, nouveau, motif, qui, quand.
 *
 * Il vit SOUS les montants plutôt que dans un onglet : trois lignes ne méritent
 * pas un onglet, et la question « pourquoi ce colis coûte-t-il 40 ? » se pose en
 * regardant le 40.
 */
export function ParcelFeeHistory({ changes }: { changes: ParcelFeeChange[] }) {
  const t = useTranslations('parcels.fees');
  const format = useFormatter();

  if (changes.length === 0) return null;

  const money = (cents: number) => format.number(cents / 100, 'currency');

  return (
    <div className="flex flex-col gap-2">
      <p className="text-muted-foreground text-xs font-medium">{t('history')}</p>

      <ul className="divide-border divide-y text-xs">
        {changes.map((change) => (
          <li key={change.id} className="flex flex-col gap-0.5 py-2">
            <span className="flex flex-wrap items-baseline gap-1.5">
              <span className="font-medium">{t(change.field)}</span>
              {/*
                La flèche est dans la TRADUCTION, pas dans le JSX : « de X à Y »
                se dit dans le bon sens en arabe, un « → » codé en dur non.
              */}
              <span className="text-muted-foreground tabular-nums">
                {t('changeFromTo', {
                  old: change.oldAmount === null ? '-' : money(change.oldAmount),
                  new: money(change.newAmount),
                })}
              </span>
            </span>

            <span className="text-muted-foreground">{change.reason}</span>

            <span className="text-muted-foreground">
              {change.authorName ? `${change.authorName} · ` : ''}
              {format.dateTime(new Date(change.createdAt), 'stamp')}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
