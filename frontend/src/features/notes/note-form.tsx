'use client';

import {
  type Agency,
  type City,
  type Client,
  type DeliveryNoteInput,
  type DeliveryNotePayload,
  deliveryNoteSchema,
  type NoteKind,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { useEffect, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { toast } from 'sonner';
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { CitySelect } from '@/components/city-select';
import { SearchableSelect } from '@/components/searchable-select';
import { FormSheet } from '@/features/referential/form-sheet';
import { useRouteError } from '@/features/routes/use-route-error';
import { createDeliveryNoteAction } from '@/lib/actions/routes';

const SAME_AS_CLIENT = '__client__';

/**
 * Ouverture d'un bon de ramassage ou de dépôt — DOMAIN.md §1.12.
 *
 * Le type n'est pas un champ : il vient de l'écran d'où l'on part. Un agent
 * qui clique « nouveau bon » depuis la liste des ramassages ne veut pas choisir
 * entre ramassage et dépôt, il veut saisir un ramassage.
 *
 * Le client, lui, peut être imposé : depuis une fiche client, le choix est déjà
 * fait, et le proposer de nouveau n'ouvre qu'une occasion de se tromper.
 */
export function NoteForm({
  kind,
  clients,
  agencies,
  cities,
  clientId,
  onDone,
  onCreated,
}: {
  kind: NoteKind;
  clients: Client[];
  agencies: Agency[];
  cities: City[];
  /** Imposé depuis une fiche client ; laissé vide, le champ s'affiche. */
  clientId?: string;
  onDone: () => void;
  onCreated: (code: string) => void;
}) {
  const t = useTranslations('notes.form');
  const tKind = useTranslations('notes.kind');
  const translateError = useRouteError();
  const [saving, setSaving] = useState(false);

  /** L'agence de la fiche du client, quand elle en a une. */
  const agencyOf = (id: string): string =>
    clients.find((client) => client.id === id)?.agencyId ?? '';

  const form = useForm<DeliveryNoteInput, unknown, DeliveryNotePayload>({
    resolver: zodResolver(deliveryNoteSchema),
    defaultValues: {
      kind,
      clientId: clientId ?? '',
      agencyId:
        (clientId ? agencyOf(clientId) : '') ||
        (agencies.length === 1 ? (agencies[0]?.id ?? '') : ''),
      locationCityId: '',
    },
    mode: 'onBlur',
  });

  /*
   * L'AGENCE SUIT LE CLIENT CHOISI — retouches Anass, point 4.
   *
   * « À la création d'un bon par l'admin, l'agence est préremplie depuis la
   * fiche du client sélectionné, changeable. » Elle ne l'était pas : sur une
   * société à quatre agences, le champ s'ouvrait vide et se remplissait de
   * mémoire, ou pas du tout.
   *
   * Le préremplissage ne s'impose PAS : il ne touche le champ que tant que
   * personne ne l'a modifié à la main (`dirtyFields`). Un agent qui a
   * délibérément choisi une autre agence, puis corrige le client, ne voit pas
   * son choix effacé.
   */
  const selectedClientId = useWatch({ control: form.control, name: 'clientId' });

  useEffect(() => {
    if (!selectedClientId) return;
    if (form.formState.dirtyFields.agencyId) return;

    const agencyId = agencyOf(selectedClientId);
    if (agencyId && agencyId !== form.getValues('agencyId')) {
      form.setValue('agencyId', agencyId);
    }
    // `agencyOf` dérive de `clients`, qui est une propriété stable de la page.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedClientId, clients, form]);

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await createDeliveryNoteAction(values);
    setSaving(false);

    if (result.ok) {
      toast.success(t('created', { code: result.data.code }));
      onDone();
      onCreated(result.data.code);
      return;
    }

    toast.error(translateError(result));
  });

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={tKind(kind)}
      description={t('hint')}
      onSubmit={submit}
      submitting={saving}
    >
      <Form {...form}>
        <form className="flex flex-col gap-4" onSubmit={submit}>
          {clientId ? null : (
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
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          )}

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
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="locationCityId"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.city')}</FormLabel>
                <FormControl>
                  <CitySelect
                    cities={cities}
                    value={field.value || SAME_AS_CLIENT}
                    onValueChange={(value) => field.onChange(value === SAME_AS_CLIENT ? '' : value)}
                    placeholder={t('fields.city')}
                    emptyOption={{ value: SAME_AS_CLIENT, label: t('fields.cityDefault') }}
                  />
                </FormControl>
                <FormDescription>{t('fields.cityHint')}</FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />
        </form>
      </Form>
    </FormSheet>
  );
}
