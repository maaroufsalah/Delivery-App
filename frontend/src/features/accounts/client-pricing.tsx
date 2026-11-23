'use client';

import {
  type City,
  type ClientDetail as ClientDetailData,
  type ClientDiscountInput,
  type ClientDiscountItem,
  type ClientDiscountPayload,
  clientDiscountSchema,
  type Zone,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { ChevronDown, Landmark, Pencil, Percent, Plus, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useCallback, useMemo, useState, useTransition } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { CityChecklist } from '@/components/city-checklist';
import { CellBadge } from '@/components/data-table/cells';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
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
import { Switch } from '@/components/ui/switch';
import { FormSheet } from '@/features/referential/form-sheet';
import {
  addClientDiscountAction,
  deleteClientDiscountAction,
  updateClientDiscountAction,
} from '@/lib/actions/accounts';
import { cn } from '@/lib/utils';

/** Centimes → dirhams pour l'affichage ; la saisie, elle, est en dirhams. */
function rate(value: number | null): number | null {
  return value === null ? null : value / 100;
}

/**
 * Le tarif société qui s'applique à ce client, sans remise — lot K, K1.
 * Calculé par la page avec le devis lui-même (`quoteDeliveryFee`), jamais ici.
 */
export interface ClientCompanyRate {
  originCityName: string | null;
  originZoneName: string | null;
  /** Colis ramassé et livré dans la ville de la fiche ; `null` = devis indisponible. */
  sameCityFee: number | null;
}

/**
 * L'ONGLET « TARIFS & REMISES » DE LA FICHE CLIENT — lot K (D-218).
 *
 * La carte des remises a quitté « Profil » telle quelle (lot H) ; au-dessus, en
 * lecture seule, ce que paie le client SANS remise, pour qu'une remise se
 * décide en regard du tarif qu'elle écrase.
 *
 * Les remises arrivent de l'API dans l'ordre de PRIORITÉ du devis : villes,
 * zones, puis « Toutes les zones ».
 */
export function ClientPricing({
  client,
  cities,
  zones,
  companyRate,
}: {
  client: ClientDetailData;
  cities: City[];
  zones: Zone[];
  companyRate: ClientCompanyRate;
}) {
  const tDetail = useTranslations('accounts.clients.detail');
  const tCommon = useTranslations('common');
  const format = useFormatter();

  // `undefined` = panneau fermé · `null` = création · objet = modification.
  const [discount, setDiscount] = useState<ClientDiscountItem | null | undefined>(undefined);
  const [, startTransition] = useTransition();
  const label = useDiscountLabel(cities, zones);

  const remove = (discountId: string) => {
    startTransition(async () => {
      const result = await deleteClientDiscountAction(client.id, discountId);
      if (!result.ok) toast.error(result.error ?? tCommon('delete'));
    });
  };

  return (
    <div className="flex flex-col gap-4">
      {/* --- Tarif société, lecture seule ------------------------------------ */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Landmark className="size-4" />
            {tDetail('companyRate')}
          </CardTitle>
          <CardDescription>{tDetail('companyRateHint')}</CardDescription>
        </CardHeader>

        <CardContent className="flex flex-col gap-3">
          <dl className="grid gap-3 text-sm sm:grid-cols-3">
            {(
              [
                ['originCity', companyRate.originCityName],
                ['companySameCityRate', companyRate.sameCityFee],
                ['originZone', companyRate.originZoneName],
              ] as const
            ).map(([key, value]) => (
              <div key={key} className="rounded-lg border p-3">
                <dt className="text-muted-foreground text-xs">{tDetail(key)}</dt>
                <dd className="font-medium tabular-nums">
                  {value === null
                    ? tDetail('companyRateUnavailable')
                    : typeof value === 'number'
                      ? format.number(value / 100, 'currency')
                      : value}
                </dd>
              </div>
            ))}
          </dl>
          <p className="bg-muted text-muted-foreground rounded-lg p-3 text-xs">
            {tDetail('priorityHint')}
          </p>
        </CardContent>
      </Card>

      {/* --- Remises ------------------------------------------------------ */}
      <Card>
        <CardHeader className="flex flex-row items-start justify-between gap-3">
          <div className="min-w-0">
            <CardTitle className="flex items-center gap-2 text-base">
              <Percent className="size-4" />
              {tDetail('discounts')}
            </CardTitle>
            <CardDescription>{tDetail('discountsHint')}</CardDescription>
          </div>
          <Button size="sm" variant="outline" onClick={() => setDiscount(null)}>
            <Plus className="size-4" />
            <span className="hidden sm:inline">{tDetail('add')}</span>
          </Button>
        </CardHeader>

        <CardContent className="flex flex-col gap-2">
          {client.discounts.length === 0 ? (
            <p className="text-muted-foreground text-sm">{tDetail('noDiscounts')}</p>
          ) : (
            client.discounts.map((entry) => (
              <div
                key={entry.id}
                className={cn(
                  'flex flex-wrap items-center gap-x-4 gap-y-1 rounded-lg border p-3 text-sm',
                  !entry.isActive && 'opacity-60',
                )}
              >
                <span className="font-medium">{label.targets(entry)}</span>

                {(
                  [
                    ['flatRate', entry.flatRate],
                    ['sameCityRate', entry.sameCityRate],
                    ['pickupFee', entry.pickupFee],
                  ] as const
                ).map(([key, value]) =>
                  value === null ? null : (
                    <span key={key} className="text-muted-foreground text-xs">
                      {tDetail(key)} :{' '}
                      <span className="text-foreground tabular-nums">
                        {format.number(rate(value) ?? 0, 'currency')}
                      </span>
                    </span>
                  ),
                )}

                {entry.isActive ? null : <CellBadge label={tCommon('inactive')} />}

                <div className="ms-auto flex items-center">
                  <Button
                    size="icon"
                    variant="ghost"
                    aria-label={tCommon('edit')}
                    onClick={() => setDiscount(entry)}
                  >
                    <Pencil className="size-4" />
                  </Button>

                  <Button
                    size="icon"
                    variant="ghost"
                    aria-label={tCommon('delete')}
                    onClick={() => remove(entry.id)}
                  >
                    <Trash2 className="text-destructive size-4" />
                  </Button>
                </div>
              </div>
            ))
          )}
        </CardContent>
      </Card>

      {discount !== undefined ? (
        <DiscountForm
          client={client}
          discount={discount}
          cities={cities}
          zones={zones}
          onDone={() => setDiscount(undefined)}
        />
      ) : null}
    </div>
  );
}

/**
 * Le libellé d'une remise : « Casablanca · Axe Casablanca – Rabat », ou
 * « Toutes les zones » ; `full` y ajoute ses montants (« … · Tarif fixe
 * 25,00 MAD »), pour le refus de doublon.
 */
function useDiscountLabel(cities: City[], zones: Zone[]) {
  const tDetail = useTranslations('accounts.clients.detail');
  const format = useFormatter();

  return useMemo(() => {
    const cityName = new Map(cities.map((city) => [city.id, city.name]));
    const zoneName = new Map(zones.map((zone) => [zone.id, zone.name]));

    const targets = (entry: ClientDiscountItem) =>
      entry.cityIds.length + entry.zoneIds.length === 0
        ? tDetail('allZones')
        : [
            ...entry.cityIds.map((id) => cityName.get(id) ?? id),
            ...entry.zoneIds.map((id) => zoneName.get(id) ?? id),
          ].join(' · ');

    const full = (entry: ClientDiscountItem) =>
      [
        targets(entry),
        ...(
          [
            ['flatRate', entry.flatRate],
            ['sameCityRate', entry.sameCityRate],
          ] as const
        ).flatMap(([key, value]) =>
          value === null ? [] : [`${tDetail(key)} ${format.number(value / 100, 'currency')}`],
        ),
      ].join(' · ');

    return { targets, full };
  }, [cities, zones, tDetail, format]);
}

/**
 * Ajouter / modifier une remise.
 *
 * Elle ÉCRASE le tarif, elle ne s'en soustrait pas (DOMAIN.md §3.1) : un champ
 * laissé vide signifie « pas de remise sur cette ligne », pas « remise de 0 ».
 * Elle vise des zones ET/OU des villes (lot K) ; ni l'une ni l'autre = toutes
 * les zones.
 */
function DiscountForm({
  client,
  discount,
  cities,
  zones,
  onDone,
}: {
  client: ClientDetailData;
  /** `null` = création. Sinon, la remise à modifier. */
  discount: ClientDiscountItem | null;
  cities: City[];
  zones: Zone[];
  onDone: () => void;
}) {
  const tDetail = useTranslations('accounts.clients.detail');
  const tList = useTranslations('cityChecklist');
  const tCommon = useTranslations('common');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);
  // Replié par défaut : la plupart des remises ne visent aucune ville.
  const [showCities, setShowCities] = useState(false);
  const label = useDiscountLabel(cities, zones);

  const form = useForm<ClientDiscountInput, unknown, ClientDiscountPayload>({
    resolver: zodResolver(clientDiscountSchema),
    defaultValues: {
      zoneIds: discount?.zoneIds ?? [],
      cityIds: discount?.cityIds ?? [],
      // Lu en centimes, saisi en dirhams — la conversion reste à la frontière.
      flatRate: rate(discount?.flatRate ?? null),
      sameCityRate: rate(discount?.sameCityRate ?? null),
      pickupFee: rate(discount?.pickupFee ?? null),
      isActive: discount?.isActive ?? true,
    },
    mode: 'onBlur',
  });

  const selectedZones = form.watch('zoneIds') ?? [];
  const selectedCityIds = form.watch('cityIds');
  const selectedCities = useMemo(() => new Set(selectedCityIds ?? []), [selectedCityIds]);

  const toggleZone = (zoneId: string, checked: boolean) => {
    const next = checked
      ? [...new Set([...selectedZones, zoneId])]
      : selectedZones.filter((entry) => entry !== zoneId);
    form.setValue('zoneIds', next, { shouldDirty: true });
  };

  const toggleCities = useCallback(
    (cityIds: string[], checked: boolean) => {
      const current = form.getValues('cityIds') ?? [];
      const next = checked
        ? [...new Set([...current, ...cityIds])]
        : current.filter((entry) => !cityIds.includes(entry));
      form.setValue('cityIds', next, { shouldDirty: true });
    },
    [form],
  );

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = discount
      ? await updateClientDiscountAction(client.id, discount.id, values)
      : await addClientDiscountAction(client.id, values);
    setSaving(false);

    if (result.ok) {
      onDone();
      return;
    }

    // Lot K (D-218) : le doublon nomme la remise existante, dans la langue de l'agent.
    const existing =
      result.reason === 'discount-duplicate'
        ? client.discounts.find((entry) => entry.id === result.discountId)
        : undefined;
    toast.error(
      existing
        ? tDetail('duplicate', { discount: label.full(existing) })
        : result.error === 'invalid'
          ? tError('invalid')
          : result.error,
    );
  });

  const checkedCities = cities.filter((city) => selectedCities.has(city.id));

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={discount ? tCommon('edit') : tDetail('add')}
      description={tDetail('discountsHint')}
      onSubmit={submit}
      submitting={saving}
    >
      <Form {...form}>
        <form className="flex flex-col gap-4" onSubmit={submit}>
          {/* Lot H (D-209), lot K (D-218) : l'ordre réel de `quoteDeliveryFee`, dit à la saisie. */}
          <p className="bg-muted text-muted-foreground rounded-lg p-3 text-xs">
            {tDetail('priorityHint')}
          </p>
          <FormField
            control={form.control}
            name="zoneIds"
            render={() => (
              <FormItem>
                <FormLabel>{tDetail('zones')}</FormLabel>
                <FormDescription>{tDetail('zonesHint')}</FormDescription>
                <div className="flex flex-col gap-1 rounded-lg border p-2">
                  {zones.map((zone) => (
                    <label
                      key={zone.id}
                      className="hover:bg-accent/60 flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm"
                    >
                      <Checkbox
                        checked={selectedZones.includes(zone.id)}
                        onCheckedChange={(checked) => toggleZone(zone.id, checked === true)}
                      />
                      <span className="truncate">{zone.name}</span>
                    </label>
                  ))}
                </div>
                <FormMessage />
              </FormItem>
            )}
          />

          {/* Lot K : les villes, sous les zones, dans le tableau du lot F. */}
          <FormField
            control={form.control}
            name="cityIds"
            render={() => (
              <FormItem>
                <div className="flex items-center justify-between gap-2">
                  <FormLabel>{tDetail('cities')}</FormLabel>
                  <span className="text-muted-foreground text-xs">
                    {tList('count', { count: selectedCities.size })}
                  </span>
                </div>
                <FormDescription>{tDetail('citiesHint')}</FormDescription>

                {/* Repliée, la liste garde ses villes cochées sous les yeux. */}
                {checkedCities.length > 0 ? (
                  <div className="flex flex-wrap gap-1.5">
                    {checkedCities.map((city) => (
                      <CellBadge key={city.id} label={city.name} />
                    ))}
                  </div>
                ) : null}

                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  className="w-fit gap-1.5"
                  aria-expanded={showCities}
                  onClick={() => setShowCities((open) => !open)}
                >
                  <ChevronDown
                    className={cn('size-4 transition-transform', showCities && 'rotate-180')}
                  />
                  {showCities ? tDetail('hideCities') : tDetail('showCities')}
                </Button>

                {showCities ? (
                  <CityChecklist
                    cities={cities}
                    zones={zones}
                    selected={selectedCities}
                    onChange={toggleCities}
                    checkHeader={tDetail('targeted')}
                  />
                ) : null}
                <FormMessage />
              </FormItem>
            )}
          />

          {(['flatRate', 'sameCityRate', 'pickupFee'] as const).map((name) => (
            <FormField
              key={name}
              control={form.control}
              name={name}
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{tDetail(name)}</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      min={0}
                      step="0.01"
                      value={field.value ?? ''}
                      onChange={(event) =>
                        field.onChange(
                          event.target.value === '' ? null : event.target.valueAsNumber,
                        )
                      }
                    />
                  </FormControl>
                  {/* Lot H : ce que couvre chaque tarif (intra-ville = départ = destination). */}
                  {name === 'pickupFee' ? null : (
                    <FormDescription>{tDetail(`${name}Hint`)}</FormDescription>
                  )}
                  <FormMessage />
                </FormItem>
              )}
            />
          ))}

          <FormField
            control={form.control}
            name="isActive"
            render={({ field }) => (
              <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                <FormLabel>{tCommon('active')}</FormLabel>
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
