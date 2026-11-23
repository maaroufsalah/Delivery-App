'use client';

import {
  type Agency,
  type City,
  type Courier,
  type RemittanceCandidate,
  type RemittanceCreateInput,
  type RemittanceCreatePayload,
  remittanceCreateSchema,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
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
import { Checkbox } from '@/components/ui/checkbox';
import { CitySelect } from '@/components/city-select';
import { SearchableSelect } from '@/components/searchable-select';
import { createRemittanceAction, remittanceCandidatesAction } from '@/lib/actions/finance';

/**
 * Ouverture d'un retour de fonds : le livreur, la ville, l'agence qui reçoit.
 *
 * La ville vient du legacy, où les recettes étaient groupées par ville
 * (`recettes.ville_id`) : un livreur qui tourne sur deux villes rapporte deux
 * enveloppes, et les compter séparément est ce qui permet de retrouver un écart.
 */
export function RemittanceForm({
  couriers,
  cities,
  agencies,
  onDone,
  onCreated,
}: {
  couriers: Courier[];
  cities: City[];
  agencies: Agency[];
  onDone: () => void;
  onCreated: (code: string) => void;
}) {
  const t = useTranslations('finance.remittances');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);

  /*
   * LES COLIS, DÈS LA CRÉATION — chantier « Retour de fonds unique », point 5.
   *
   * Un bon ne s'ouvre plus vide : il occuperait la place du SEUL bon ouvert que
   * le livreur peut avoir, et empêcherait l'ouverture automatique de
   * fonctionner pour lui. Le formulaire propose donc ses colis remettables dès
   * qu'un livreur est choisi.
   */
  const [candidates, setCandidates] = useState<RemittanceCandidate[]>([]);
  const [picked, setPicked] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);

  const form = useForm<RemittanceCreateInput, unknown, RemittanceCreatePayload>({
    resolver: zodResolver(remittanceCreateSchema),
    defaultValues: {
      courierId: '',
      cityId: '',
      agencyId: agencies.length === 1 ? (agencies[0]?.id ?? '') : '',
      note: '',
    },
    mode: 'onBlur',
  });

  const courierId = form.watch('courierId');

  useEffect(() => {
    let alive = true;
    setPicked([]);

    if (!courierId) {
      setCandidates([]);
      return;
    }

    setLoading(true);
    void remittanceCandidatesAction(courierId).then((result) => {
      if (!alive) return;
      setLoading(false);
      setCandidates(result.ok ? result.data : []);
    });

    return () => {
      alive = false;
    };
  }, [courierId]);

  const submit = form.handleSubmit(async (values) => {
    if (picked.length === 0) {
      toast.error(t('errors.needsParcels'));
      return;
    }

    setSaving(true);
    const result = await createRemittanceAction({ ...values, parcelIds: picked });
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
            name="courierId"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.courier')}</FormLabel>
                <FormControl>
                  <SearchableSelect
                    options={couriers.map((courier) => ({
                      value: courier.id,
                      label: `${courier.firstName} ${courier.lastName}`,
                    }))}
                    value={field.value}
                    onValueChange={field.onChange}
                    placeholder={t('fields.courier')}
                    className="w-full"
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

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

          {/*
            Les colis remettables du livreur. La liste est vide tant qu'aucun
            livreur n'est choisi — proposer des cases à cocher sans savoir à qui
            elles appartiennent n'aurait rien voulu dire.
          */}
          {courierId ? (
            <div className="flex flex-col gap-2">
              <p className="text-sm font-medium">{t('fields.parcels')}</p>

              {loading ? (
                <p className="text-muted-foreground text-sm">{t('loadingParcels')}</p>
              ) : candidates.length === 0 ? (
                <p className="text-muted-foreground text-sm">{t('noCandidates')}</p>
              ) : (
                <ul className="max-h-56 overflow-y-auto rounded-md border">
                  {candidates.map((parcel) => (
                    <li
                      key={parcel.id}
                      className="flex items-center gap-3 border-b p-2 last:border-0"
                    >
                      <Checkbox
                        checked={picked.includes(parcel.id)}
                        onCheckedChange={(checked) =>
                          setPicked((current) =>
                            checked
                              ? [...current, parcel.id]
                              : current.filter((id) => id !== parcel.id),
                          )
                        }
                        aria-label={parcel.code}
                      />
                      <span className="min-w-0 flex-1 truncate text-sm">
                        <span className="font-mono">{parcel.code}</span>{' '}
                        <span className="text-muted-foreground">{parcel.recipientName}</span>
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          ) : null}

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
