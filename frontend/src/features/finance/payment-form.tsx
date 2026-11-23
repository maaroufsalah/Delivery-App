'use client';

import {
  type Agency,
  type BankAccount,
  type Client,
  type InvoiceDetail,
  PAYMENT_METHODS,
  type PaymentInput,
  type PaymentListItem,
  type PaymentPayload,
  paymentSchema,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTimeZone, useTranslations } from 'next-intl';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { SearchableSelect } from '@/components/searchable-select';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { FormSheet } from '@/features/referential/form-sheet';
import { useFinanceError } from '@/features/finance/use-finance-error';
import { createPaymentAction } from '@/lib/actions/finance';
import { localToday } from '@/lib/local-day';

/**
 * Enregistrement d'un règlement.
 *
 * Deux usages, un seul formulaire : rattaché à une facture — depuis sa fiche,
 * l'e-commerçant et l'agence sont alors imposés et le montant pré-rempli au
 * solde — ou libre, depuis la liste des règlements, pour un acompte.
 *
 * Le montant est saisi en DIRHAMS, comme partout ailleurs : c'est ce que
 * l'opérateur lit sur le reçu qu'il a en main. La conversion en centimes se
 * fait au bord, côté API.
 */
export function PaymentForm({
  accounts,
  invoice,
  clients,
  agencies,
  onDone,
  onCreated,
}: {
  accounts: BankAccount[];
  /** Fiche d'une facture : le contexte est imposé et le solde pré-rempli. */
  invoice?: InvoiceDetail;
  clients?: Client[];
  agencies?: Agency[];
  onDone: () => void;
  onCreated?: (payment: PaymentListItem) => void;
}) {
  const t = useTranslations('finance.payments');
  const timeZone = useTimeZone();
  const tMethod = useTranslations('finance.payments.method');
  const tError = useTranslations('validation');
  const errorMessage = useFinanceError();
  const [saving, setSaving] = useState(false);

  const form = useForm<PaymentInput, unknown, PaymentPayload>({
    resolver: zodResolver(paymentSchema),
    defaultValues: {
      invoiceId: invoice?.id ?? '',
      clientId: invoice?.clientId ?? '',
      agencyId: invoice?.agencyId ?? (agencies?.length === 1 ? (agencies[0]?.id ?? '') : ''),
      // Le solde restant, en dirhams : le cas courant est « il paie tout ».
      amount: invoice ? Math.abs(invoice.balance) / 100 : undefined,
      method: 'cash',
      // Le compte par défaut, quand l'administration en a désigné un : la
      // saisie d'un virement est déjà assez longue (Paramètres > Finance).
      bankAccountId: accounts.find((account) => account.isDefault)?.id ?? '',
      reference: '',
      note: '',
      // Aujourd'hui au sens de la SOCIÉTÉ, pas de Greenwich (D-135).
      paidAt: localToday(timeZone),
    },
    mode: 'onBlur',
  });

  const method = form.watch('method');

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await createPaymentAction(values);
    setSaving(false);

    if (result.ok) {
      toast.success(t('created'));
      onCreated?.(result.data);
      onDone();
      return;
    }

    toast.error(result.error === 'invalid' ? tError('invalid') : errorMessage(result));
  });

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={t('new')}
      description={invoice ? t('formHintInvoice', { code: invoice.code }) : t('formHint')}
      onSubmit={submit}
      submitting={saving}
    >
      <Form {...form}>
        <form className="flex flex-col gap-4" onSubmit={submit}>
          {invoice ? null : (
            <FormField
              control={form.control}
              name="clientId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.client')}</FormLabel>
                  <FormControl>
                    <SearchableSelect
                      options={(clients ?? []).map((client) => ({
                        value: client.id,
                        label: client.name,
                      }))}
                      value={field.value}
                      onValueChange={field.onChange}
                      placeholder={t('fields.client')}
                      className="w-full"
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          )}

          {invoice ? null : (
            <FormField
              control={form.control}
              name="agencyId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.agency')}</FormLabel>
                  <FormControl>
                    <SearchableSelect
                      options={(agencies ?? []).map((agency) => ({
                        value: agency.id,
                        label: agency.name,
                      }))}
                      value={field.value}
                      onValueChange={field.onChange}
                      placeholder={t('fields.agency')}
                      className="w-full"
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          )}

          <FormField
            control={form.control}
            name="amount"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.amount')}</FormLabel>
                <FormControl>
                  <Input
                    type="number"
                    inputMode="decimal"
                    step="0.01"
                    min="0"
                    value={field.value === undefined ? '' : String(field.value)}
                    onChange={(event) =>
                      field.onChange(
                        event.target.value === '' ? undefined : Number(event.target.value),
                      )
                    }
                    onBlur={field.onBlur}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="method"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.method')}</FormLabel>
                <Select value={field.value} onValueChange={field.onChange}>
                  <FormControl>
                    <SelectTrigger>
                      <SelectValue placeholder={t('fields.method')} />
                    </SelectTrigger>
                  </FormControl>
                  <SelectContent>
                    {PAYMENT_METHODS.map((value) => (
                      <SelectItem key={value} value={value}>
                        {tMethod(value)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FormMessage />
              </FormItem>
            )}
          />

          {/* Un virement ou un chèque arrive SUR un compte ; l'espèce est
              comptée au guichet. Le champ n'apparaît que s'il a un sens. */}
          {method === 'cash' ? null : (
            <FormField
              control={form.control}
              name="bankAccountId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.account')}</FormLabel>
                  <FormControl>
                    <SearchableSelect
                      options={accounts.map((account) => ({
                        value: account.id,
                        label: `${account.label}, ${account.bankName}`,
                        keywords: [account.bankName],
                      }))}
                      value={field.value ?? ''}
                      onValueChange={field.onChange}
                      placeholder={t('fields.account')}
                      className="w-full"
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          )}

          <FormField
            control={form.control}
            name="reference"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.reference')}</FormLabel>
                <FormControl>
                  <Input {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="paidAt"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.paidAt')}</FormLabel>
                <FormControl>
                  <Input type="date" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="note"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.note')}</FormLabel>
                <FormControl>
                  <Input {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
        </form>
      </Form>
    </FormSheet>
  );
}
