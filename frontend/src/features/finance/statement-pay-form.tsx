'use client';

import { type BankAccount, PAYMENT_METHODS, type PaymentMethod } from '@delivery/shared';
import { Loader2, Wallet } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { SearchableSelect } from '@/components/searchable-select';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

/**
 * Le règlement d'un relevé livreur — C11 bloc 7, docs/DECISIONS.md D-067.
 *
 * **Aucun MONTANT ne se saisit ici**, et c'est la règle du bloc 6 : le relevé
 * dit ce qui est dû (D-065), le formulaire dit seulement par quel canal on le
 * verse. Un champ « montant » rouvrirait la négociation que tout le relevé
 * s'emploie à fermer.
 *
 * Les trois champs de chèque n'apparaissent qu'en mode chèque. Les afficher
 * grisés en permanence ferait croire qu'ils comptent pour un virement.
 */
export function StatementPayForm({
  amount,
  accounts,
  pending,
  onCancel,
  onSubmit,
}: {
  /** Ce qui est dû, ANNONCÉ et non saisi : l'opérateur confirme un montant. */
  amount: string;
  accounts: BankAccount[];
  pending: boolean;
  onCancel: () => void;
  onSubmit: (payload: {
    method: PaymentMethod;
    bankAccountId: string;
    reference: string;
    chequeNumber: string;
    chequeBank: string;
    chequeDate: string;
  }) => void;
}) {
  const t = useTranslations('finance.statements');
  const tMethod = useTranslations('finance.payments.method');
  const tCommon = useTranslations('common');

  const [method, setMethod] = useState<PaymentMethod>('cash');
  const [bankAccountId, setBankAccountId] = useState(
    accounts.find((account) => account.isDefault)?.id ?? '',
  );
  const [reference, setReference] = useState('');
  const [chequeNumber, setChequeNumber] = useState('');
  const [chequeBank, setChequeBank] = useState('');
  const [chequeDate, setChequeDate] = useState('');

  const cheque = method === 'check';
  // Le numéro est LE champ qui permet de retrouver un chèque revenu impayé.
  const incomplete =
    (cheque && chequeNumber.trim() === '') || (method !== 'cash' && !bankAccountId);

  return (
    <Dialog open onOpenChange={(next) => !next && onCancel()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('pay')}</DialogTitle>
          <DialogDescription>{t('payHint', { amount })}</DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="statement-method">{t('fields.method')}</Label>
            <Select value={method} onValueChange={(value) => setMethod(value as PaymentMethod)}>
              <SelectTrigger id="statement-method">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {PAYMENT_METHODS.map((option) => (
                  <SelectItem key={option} value={option}>
                    {tMethod(option)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {method === 'cash' ? null : (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="statement-account">{t('fields.account')}</Label>
              <SearchableSelect
                options={accounts.map((account) => ({ value: account.id, label: account.label }))}
                value={bankAccountId}
                onValueChange={setBankAccountId}
                placeholder={t('fields.account')}
                id="statement-account"
                className="w-full"
              />
            </div>
          )}

          {cheque ? (
            <>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="cheque-number">{t('fields.chequeNumber')}</Label>
                  <Input
                    id="cheque-number"
                    value={chequeNumber}
                    onChange={(event) => setChequeNumber(event.target.value)}
                  />
                </div>

                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="cheque-bank">{t('fields.chequeBank')}</Label>
                  <Input
                    id="cheque-bank"
                    value={chequeBank}
                    onChange={(event) => setChequeBank(event.target.value)}
                  />
                </div>
              </div>

              <div className="flex flex-col gap-1.5">
                <Label htmlFor="cheque-date">{t('fields.chequeDate')}</Label>
                <Input
                  id="cheque-date"
                  type="date"
                  value={chequeDate}
                  onChange={(event) => setChequeDate(event.target.value)}
                />
                {/*
                  La date d'ÉMISSION, pas celle du règlement. Un chèque
                  post-daté est reçu aujourd'hui et encaissable dans trois
                  semaines ; confondre les deux fait rapprocher une caisse qui
                  ne tombera jamais.
                */}
                <p className="text-muted-foreground text-xs">{t('chequeDateHint')}</p>
              </div>
            </>
          ) : (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="statement-reference">{t('fields.reference')}</Label>
              <Input
                id="statement-reference"
                value={reference}
                onChange={(event) => setReference(event.target.value)}
              />
            </div>
          )}

          {/*
            Le livreur photographiera son chèque depuis SON téléphone (D-068) :
            l'agent n'a pas à le faire pour lui, et le dire ici évite qu'il
            cherche un champ de téléversement qui n'existe pas.
          */}
          {cheque ? (
            <p className="text-muted-foreground border-border border-t pt-3 text-xs">
              {t('chequePhotoHint')}
            </p>
          ) : null}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onCancel}>
            {tCommon('cancel')}
          </Button>
          <Button
            disabled={pending || incomplete}
            className="gap-1.5"
            onClick={() =>
              onSubmit({ method, bankAccountId, reference, chequeNumber, chequeBank, chequeDate })
            }
          >
            {pending ? <Loader2 className="size-4 animate-spin" /> : <Wallet className="size-4" />}
            {t('pay')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
