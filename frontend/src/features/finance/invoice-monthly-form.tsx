'use client';

import {
  type Agency,
  type Client,
  type InvoiceClientKind,
  type InvoiceMonthlyInput,
  type InvoiceMonthlyPayload,
  invoiceMonthlySchema,
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
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { FormSheet } from '@/features/referential/form-sheet';
import { useFinanceError } from '@/features/finance/use-finance-error';
import { createMonthlyInvoiceAction } from '@/lib/actions/finance';
import { localToday, previousMonth } from '@/lib/local-day';

/**
 * Le mois révolu, en bornes `AAAA-MM-JJ` — la période qu'on facture neuf fois
 * sur dix. Compté depuis le jour de la SOCIÉTÉ (D-135) : le 1er à 00 h 30 à
 * Casablanca, « le mois dernier » ne recule plus de deux mois.
 */
function lastMonth(today: string): { periodStart: string; periodEnd: string } {
  const { start, end } = previousMonth(today);
  return { periodStart: start, periodEnd: end };
}

/**
 * « Générer la facture du mois » — C11 bloc 3, D-061.
 *
 * Un e-commerçant, une période, et c'est tout : le service ramasse lui-même
 * les colis facturables et compose le brouillon. C'est ce qui distingue ce
 * formulaire de `InvoiceForm`, où l'opérateur choisit ensuite colis par colis —
 * un client professionnel en a cent par mois, et les cocher un par un est
 * exactement le travail que le PO veut supprimer.
 *
 * La liste ne contient que des PROFESSIONNELS : un client normal se facture à
 * la composition, parce que sa facture déduit les frais du COD qu'elle
 * encaisse. L'API le refuserait de toute façon.
 *
 * La période par défaut est le mois RÉVOLU, jamais le mois courant : on ne
 * facture pas un mois qui n'est pas fini, et les échecs n'y sont pas encore
 * facturables (D-061).
 */
export function InvoiceMonthlyForm({
  clients,
  agencies,
  onDone,
  onCreated,
  kind = 'normal',
}: {
  /** Seulement les PROFESSIONNELS. */
  clients: Client[];
  agencies: Agency[];
  onDone: () => void;
  onCreated: (code: string) => void;
  /** Le type de la page — recette 3, point 4 : la porte d'entrée de l'API. */
  kind?: InvoiceClientKind;
}) {
  const t = useTranslations('finance.invoices');
  const timeZone = useTimeZone();
  const errorMessage = useFinanceError();
  const [saving, setSaving] = useState(false);

  const form = useForm<InvoiceMonthlyInput, unknown, InvoiceMonthlyPayload>({
    resolver: zodResolver(invoiceMonthlySchema),
    defaultValues: {
      clientId: '',
      agencyId: agencies.length === 1 ? (agencies[0]?.id ?? '') : '',
      ...lastMonth(localToday(timeZone)),
      note: '',
    },
    mode: 'onBlur',
  });

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await createMonthlyInvoiceAction(values, kind);
    setSaving(false);

    if (result.ok) {
      toast.success(t('monthlyCreated', { code: result.data.code }));
      onDone();
      onCreated(result.data.code);
      return;
    }

    toast.error(errorMessage(result));
  });

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={t('monthly')}
      description={t('monthlyHint')}
      onSubmit={submit}
      submitting={saving}
    >
      <Form {...form}>
        <form className="flex flex-col gap-4" onSubmit={submit}>
          <FormField
            control={form.control}
            name="clientId"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.client')}</FormLabel>
                <FormControl>
                  <SearchableSelect
                    options={clients.map((client) => ({ value: client.id, label: client.name }))}
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

          <FormField
            control={form.control}
            name="agencyId"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.agency')}</FormLabel>
                <FormControl>
                  <SearchableSelect
                    options={agencies.map((agency) => ({ value: agency.id, label: agency.name }))}
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

          <div className="grid gap-4 sm:grid-cols-2">
            <FormField
              control={form.control}
              name="periodStart"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.periodStart')}</FormLabel>
                  <FormControl>
                    <Input type="date" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="periodEnd"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.periodEnd')}</FormLabel>
                  <FormControl>
                    <Input type="date" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <FormField
            control={form.control}
            name="note"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.note')}</FormLabel>
                <FormControl>
                  <Input {...field} />
                </FormControl>
                {/*
                  LA phrase qui évite le malentendu du régime : la facture d'un
                  professionnel ne reverse rien, elle réclame les frais. Son COD
                  lui a été versé à part, par bon de versement.
                */}
                <FormDescription>{t('monthlyFeesOnlyHint')}</FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />
        </form>
      </Form>
    </FormSheet>
  );
}
