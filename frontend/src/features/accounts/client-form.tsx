'use client';

import {
  type Agency,
  BILLING_TYPES,
  type City,
  CLIENT_TYPES,
  type ClientDetail,
  type ClientInput,
  type ClientPayload,
  clientSchema,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { ALL_VALUE } from '@/components/data-table/data-table-toolbar';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { CitySelect } from '@/components/city-select';
import { SearchableSelect } from '@/components/searchable-select';
import { FormSheet } from '@/features/referential/form-sheet';
import { saveClientAction } from '@/lib/actions/accounts';

/**
 * Formulaire de fiche e-commerçant, partagé par la création (depuis la liste)
 * et la modification (depuis la fiche).
 *
 * Il attend un `ClientDetail`, jamais une ligne de liste : l'adresse et les
 * notes internes ne descendent qu'avec la fiche, et un formulaire qui ne les
 * connaît pas les effacerait à l'enregistrement.
 */
export function ClientForm({
  client,
  cities,
  agencies,
  onDone,
}: {
  client: ClientDetail | null;
  cities: City[];
  agencies: Agency[];
  onDone: () => void;
}) {
  const t = useTranslations('accounts.clients');
  const tType = useTranslations('accounts.clients.types');
  const tBilling = useTranslations('accounts.clients.billingTypes');
  // Namespace dédié : une clé composée à la volée — `fields.billingTypeHint.${x}`
  // — n'est pas typable par next-intl, et le typage strict des clés est la
  // règle n°4 de CLAUDE.md.
  const tBillingHint = useTranslations('accounts.clients.fields.billingTypeHint');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);

  const form = useForm<ClientInput, unknown, ClientPayload>({
    resolver: zodResolver(clientSchema),
    defaultValues: {
      name: client?.name ?? '',
      legalName: client?.legalName ?? '',
      type: client?.type ?? 'merchant',
      taxId: client?.taxId ?? '',
      phone: client?.phone ?? '',
      email: client?.email ?? '',
      cityId: client?.cityId ?? '',
      agencyId: client?.agencyId ?? '',
      address: client?.address ?? '',
      notes: client?.notes ?? '',
      billingType: client?.billingType ?? 'normal',
      monthlyBilling: client?.monthlyBilling ?? false,
      isPro: client?.isPro ?? false,
      isActive: client?.isActive ?? true,
    },
    mode: 'onBlur',
  });

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await saveClientAction(client?.id ?? null, values);
    setSaving(false);

    if (result.ok) {
      toast.success(
        client ? t('updated', { name: values.name }) : t('created', { name: values.name }),
      );
      onDone();
      return;
    }

    if (result.field === 'email') form.setError('email', { message: result.error });
    else if (result.field === 'taxId') form.setError('taxId', { message: result.error });
    else toast.error(result.error === 'invalid' ? tError('invalid') : result.error);
  });

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={client ? t('edit') : t('new')}
      description={t('formHint')}
      onSubmit={submit}
      submitting={saving}
    >
      <Form {...form}>
        <form className="flex flex-col gap-4" onSubmit={submit}>
          <FormField
            control={form.control}
            name="name"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.name')}</FormLabel>
                <FormControl>
                  <Input {...field} autoFocus />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <div className="grid grid-cols-2 gap-3">
            <FormField
              control={form.control}
              name="legalName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.legalName')}</FormLabel>
                  <FormControl>
                    <Input {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="type"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.type')}</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {CLIENT_TYPES.map((type) => (
                        <SelectItem key={type} value={type}>
                          {tType(type)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <FormField
            control={form.control}
            name="taxId"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.taxId')}</FormLabel>
                <FormControl>
                  <Input {...field} inputMode="numeric" maxLength={15} />
                </FormControl>
                <FormDescription>{t('fields.taxIdHint')}</FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />

          <div className="grid grid-cols-2 gap-3">
            <FormField
              control={form.control}
              name="phone"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.phone')}</FormLabel>
                  <FormControl>
                    <Input {...field} inputMode="tel" />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="email"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.email')}</FormLabel>
                  <FormControl>
                    <Input {...field} type="email" />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <FormField
              control={form.control}
              name="cityId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.city')}</FormLabel>
                  <FormControl>
                    <CitySelect
                      cities={cities}
                      value={field.value}
                      onValueChange={field.onChange}
                      placeholder={t('fields.city')}
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
                      value={field.value || ALL_VALUE}
                      onValueChange={(value) => field.onChange(value === ALL_VALUE ? '' : value)}
                      placeholder={t('fields.noAgency')}
                      emptyOption={{ value: ALL_VALUE, label: t('fields.noAgency') }}
                    />
                  </FormControl>
                  {/*
                    AGENCE PAR DÉFAUT — retouches Anass, point 4.

                    Le champ existait ; ce qu'il valait, non. Laissé vide, la
                    fiche prenait l'agence de CELUI QUI SAISIT : un client de
                    Marrakech saisi par le bureau de Casablanca naissait
                    rattaché à Casablanca. Il est désormais déduit de la VILLE,
                    et cette ligne le dit à qui remplit le formulaire.
                  */}
                  <FormDescription>{t('fields.agencyHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <FormField
            control={form.control}
            name="address"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.address')}</FormLabel>
                <FormControl>
                  <Input {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="notes"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.notes')}</FormLabel>
                <FormControl>
                  <Textarea {...field} rows={3} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          {/*
            C11 bloc 3, D-061 — le RÉGIME de facturation.

            Il décide de la forme du règlement, pas d'un détail d'affichage :
            un client `professional` touche son COD en entier par bon de
            versement et reçoit ses frais sur une facture mensuelle. Le
            changer est un acte de gestion, d'où la description sous le champ
            plutôt qu'un intitulé sec.
          */}
          <FormField
            control={form.control}
            name="billingType"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.billingType')}</FormLabel>
                <Select value={field.value} onValueChange={field.onChange}>
                  <FormControl>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                  </FormControl>
                  <SelectContent>
                    {BILLING_TYPES.map((type) => (
                      <SelectItem key={type} value={type}>
                        {tBilling(type)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FormDescription>{tBillingHint(field.value ?? 'normal')}</FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />

          {/*
            INDÉPENDANT du régime (D-019) : un client normal peut vouloir un
            récapitulatif mensuel, un pro ses frais à la semaine. Les croiser
            en une seule liste aurait demandé quatre valeurs dont personne ne
            saurait dire laquelle veut dire quoi.
          */}
          <FormField
            control={form.control}
            name="monthlyBilling"
            render={({ field }) => (
              <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                <div className="flex flex-col gap-0.5">
                  <FormLabel>{t('fields.monthlyBilling')}</FormLabel>
                  <FormDescription>{t('fields.monthlyBillingHint')}</FormDescription>
                </div>
                <FormControl>
                  <Switch checked={field.value} onCheckedChange={field.onChange} />
                </FormControl>
              </FormItem>
            )}
          />

          {/*
            CLIENT PRO — chantier « Retours, BL Clients Pro », point 4.
            « Booléen isPro sur la fiche e-commerçant (admin seul), affiché
            Client Pro. Toute la fonctionnalité BL n'existe que si isPro. »

            **Ce n'est PAS le régime de facturation**, et le placer juste
            au-dessous le dit mieux qu'une note : `professional` décide de la
            FACTURE (D-061), celui-ci des BONS DE LIVRAISON à rapporter. Un
            client peut vouloir l'un sans l'autre.
          */}
          <FormField
            control={form.control}
            name="isPro"
            render={({ field }) => (
              <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                <div className="flex flex-col gap-0.5">
                  <FormLabel>{t('fields.isPro')}</FormLabel>
                  <FormDescription>{t('fields.isProHint')}</FormDescription>
                </div>
                <FormControl>
                  <Switch checked={field.value} onCheckedChange={field.onChange} />
                </FormControl>
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="isActive"
            render={({ field }) => (
              <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                <div className="flex flex-col gap-0.5">
                  <FormLabel>{t('fields.active')}</FormLabel>
                  <FormDescription>{t('fields.activeHint')}</FormDescription>
                </div>
                <FormControl>
                  <Switch checked={field.value} onCheckedChange={field.onChange} />
                </FormControl>
              </FormItem>
            )}
          />
        </form>
      </Form>
    </FormSheet>
  );
}
