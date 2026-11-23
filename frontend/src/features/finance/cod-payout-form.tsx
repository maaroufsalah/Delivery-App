'use client';

import {
  type Agency,
  type Client,
  type CodPayoutCreateInput,
  type CodPayoutCreatePayload,
  codPayoutCreateSchema,
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
import { FormSheet } from '@/features/referential/form-sheet';
import { createCodPayoutAction } from '@/lib/actions/finance';

/**
 * Ouverture d'un bon de versement : l'e-commerçant, l'agence qui verse.
 *
 * Ni mode de paiement ni référence ici — on ne sait pas par quel canal on
 * paiera tant qu'on n'a pas fini de composer le bon. Ils se saisissent au
 * moment du versement, qui est aussi le geste qui fige (D-061).
 *
 * La liste ne contient que des clients PROFESSIONNELS : un client normal est
 * réglé par sa facture, qui déduit déjà les frais du COD. L'API le refuserait
 * de toute façon ; ne pas le proposer évite d'avoir à expliquer le refus.
 */
export function CodPayoutForm({
  clients,
  agencies,
  onDone,
  onCreated,
}: {
  clients: Client[];
  agencies: Agency[];
  onDone: () => void;
  onCreated: (code: string) => void;
}) {
  const t = useTranslations('finance.codPayouts');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);

  const form = useForm<CodPayoutCreateInput, unknown, CodPayoutCreatePayload>({
    resolver: zodResolver(codPayoutCreateSchema),
    defaultValues: {
      clientId: '',
      agencyId: agencies.length === 1 ? (agencies[0]?.id ?? '') : '',
      note: '',
    },
    mode: 'onBlur',
  });

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await createCodPayoutAction(values);
    setSaving(false);

    if (result.ok) {
      toast.success(t('created', { code: result.data.code }));
      onDone();
      onCreated(result.data.code);
      return;
    }

    toast.error(result.error === 'invalid' ? tError('invalid') : result.error);
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
