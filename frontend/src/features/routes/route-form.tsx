'use client';

import {
  type City,
  type Courier,
  ROUTE_KINDS,
  type RouteInput,
  type RoutePayload,
  type RouteListItem,
  routeSchema,
  routeTargetsSender,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
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
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { CitySelect } from '@/components/city-select';
import { SearchableSelect } from '@/components/searchable-select';
import { FormSheet } from '@/features/referential/form-sheet';
import { useRouteError } from '@/features/routes/use-route-error';
import { createRouteAction, updateRouteAction } from '@/lib/actions/routes';

/**
 * Ouverture d'un circuit : un livreur, une ville, rien de plus.
 *
 * La ville n'est pas décorative — c'est elle qui décide des colis éligibles,
 * et donc de tout l'écran suivant. Le circuit naît en brouillon : il se remplit
 * ensuite, et ne part que lorsqu'on le décide (DOMAIN.md §1.13).
 */
export function RouteForm({
  cities,
  couriers,
  onDone,
  onCreated,
  route,
  locked = false,
}: {
  cities: City[];
  couriers: Courier[];
  onDone: () => void;
  onCreated?: (code: string) => void;
  /**
   * MODIFIER UN BROUILLON — D-173 (lot B, point B2). Le même formulaire,
   * prérempli ; l'API refuse un circuit parti quoi que l'écran envoie.
   */
  route?: Pick<RouteListItem, 'code' | 'cityId' | 'courierId' | 'kind' | 'exitDate'>;
  /** Brouillon non vide : ville et type figés (l'API les refuse aussi, `route-not-empty`). */
  locked?: boolean;
}) {
  const t = useTranslations('routes.form');
  const tKind = useTranslations('routeKind');
  const translateError = useRouteError();
  const [saving, setSaving] = useState(false);

  const form = useForm<RouteInput, unknown, RoutePayload>({
    resolver: zodResolver(routeSchema),
    defaultValues: route
      ? {
          cityId: route.cityId,
          courierId: route.courierId,
          kind: route.kind,
          exitDate: route.exitDate ? route.exitDate.slice(0, 10) : '',
        }
      : { cityId: '', courierId: '', kind: 'delivery', exitDate: '' },
    mode: 'onBlur',
  });

  /*
   * Le TYPE change le SENS de la ville — C12 bloc 3.
   *
   * Une tournée de livraison dessert une ville de destination ; un ramassage
   * ou un retour va chez l'expéditeur, donc dans sa ville d'origine. Le dire
   * sous le champ évite qu'un agent compose une tournée de ramassage à
   * Casablanca en pensant aux colis qui y vont.
   */
  const kind = useWatch({ control: form.control, name: 'kind' }) ?? 'delivery';
  const cityHint = routeTargetsSender(kind) ? 'fields.cityHintSender' : 'fields.cityHint';

  /*
   * LES VILLES DU LIVREUR — lot F, F2 (D-206). Un livreur qui a des villes
   * cochées n'a pas de ville à choisir : son circuit propose les colis de
   * toutes ses villes, et le serveur pose la ville de sa fiche. Sans ville
   * cochée : le choix d'avant, avec l'avertissement de sa fiche.
   */
  const courierId = useWatch({ control: form.control, name: 'courierId' });
  const chosen = couriers.find((courier) => courier.id === courierId);
  const servedCount = chosen?.cityCount ?? 0;

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = route
      ? await updateRouteAction(route.code, values)
      : await createRouteAction(values);
    setSaving(false);

    if (result.ok) {
      toast.success(t(route ? 'saved' : 'created', { code: result.data.code }));
      onDone();
      onCreated?.(result.data.code);
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
      title={route ? t('editTitle', { code: route.code }) : t('title')}
      description={route ? t('editHint') : t('hint')}
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
                    onValueChange={(next) => {
                      field.onChange(next);
                      // Un livreur à villes : la ville de sa fiche, posée pour le schéma.
                      const picked = couriers.find((courier) => courier.id === next);
                      if ((picked?.cityCount ?? 0) > 0 && picked && !locked) {
                        form.setValue('cityId', picked.cityId, { shouldValidate: true });
                      }
                    }}
                    placeholder={t('fields.courier')}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          {servedCount > 0 ? (
            <div className="flex flex-col gap-1">
              <span className="text-sm font-medium">
                {t('fields.courierCities', { count: servedCount })}
              </span>
              <p className="text-muted-foreground text-sm">{t('fields.courierCitiesHint')}</p>
            </div>
          ) : (
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
                      disabled={locked}
                    />
                  </FormControl>
                  <FormDescription>
                    {locked
                      ? t('lockedHint')
                      : chosen
                        ? `${t('fields.courierNoCity')} ${t(cityHint)}`
                        : t(cityHint)}
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          )}

          <FormField
            control={form.control}
            name="kind"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.kind')}</FormLabel>
                <Select value={field.value} onValueChange={field.onChange} disabled={locked}>
                  <FormControl>
                    <SelectTrigger>
                      <SelectValue placeholder={t('fields.kind')} />
                    </SelectTrigger>
                  </FormControl>
                  <SelectContent>
                    {ROUTE_KINDS.map((kind) => (
                      <SelectItem key={kind} value={kind}>
                        {tKind(kind)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="exitDate"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.exitDate')}</FormLabel>
                <FormControl>
                  <Input type="date" {...field} />
                </FormControl>
                <FormDescription>{t('fields.exitDateHint')}</FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />
        </form>
      </Form>
    </FormSheet>
  );
}
