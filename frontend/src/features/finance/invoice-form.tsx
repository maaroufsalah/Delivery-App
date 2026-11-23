'use client';

import {
  type Agency,
  type Client,
  type InvoiceClientKind,
  type InvoiceCreateInput,
  type InvoiceCreatePayload,
  invoiceCreateSchema,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
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
import { useFinanceError } from '@/features/finance/use-finance-error';
import { FormSheet } from '@/features/referential/form-sheet';
import { createInvoiceAction } from '@/lib/actions/finance';

/**
 * Ouverture d'une facture : l'e-commerçant, l'agence, et on va composer.
 *
 * Aucun colis n'est demandé ici, et aucun montant. Les colis se choisissent à
 * l'écran suivant dans une liste qui sait lesquels sont éligibles ; les
 * montants se calculent. Un formulaire qui laisserait saisir un total rendrait
 * la facture négociable ligne par ligne — c'est la dette n°17 du legacy.
 */
export function InvoiceForm({
  clients,
  agencies,
  onDone,
  onCreated,
  kind = 'normal',
}: {
  clients: Client[];
  agencies: Agency[];
  onDone: () => void;
  onCreated: (code: string) => void;
  /** Le type de la page — recette 3, point 4 : la porte d'entrée de l'API. */
  kind?: InvoiceClientKind;
}) {
  const t = useTranslations('finance.invoices');
  // Lot D, D5 : le refus « brouillon déjà ouvert » se traduit, comme les autres motifs de facturation.
  const errorMessage = useFinanceError();
  const [saving, setSaving] = useState(false);

  const form = useForm<InvoiceCreateInput, unknown, InvoiceCreatePayload>({
    resolver: zodResolver(invoiceCreateSchema),
    defaultValues: {
      clientId: '',
      agencyId: agencies.length === 1 ? (agencies[0]?.id ?? '') : '',
      note: '',
    },
    mode: 'onBlur',
  });

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await createInvoiceAction(values, kind);
    setSaving(false);

    if (result.ok) {
      toast.success(t('created', { code: result.data.code }));
      onDone();
      onCreated(result.data.code);
      return;
    }

    /*
     * UN BROUILLON EST DÉJÀ OUVERT — finitions recette 8, F1. L'API refuse
     * d'en ouvrir un second (D-186, 409 `draft-exists`) et nomme le sien :
     * l'écran l'ouvre, au lieu d'un refus qui laisserait le chercher.
     */
    if (result.reason === 'draft-exists' && result.invoiceCode) {
      toast.info(t('draftAlreadyOpen', { code: result.invoiceCode }));
      onDone();
      onCreated(result.invoiceCode);
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
      title={t('new')}
      description={t('formHint')}
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
