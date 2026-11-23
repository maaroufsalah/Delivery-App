'use client';

import {
  type Agency,
  type City,
  type CourierDetail,
  type CourierInput,
  type CourierPayload,
  courierSchema,
  PAY_MODELS,
  PAY_PERIODS,
  SETTLEMENT_MODES,
  VEHICLE_TYPES,
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
import { CitySelect } from '@/components/city-select';
import { SearchableSelect } from '@/components/searchable-select';
import { FormSheet } from '@/features/referential/form-sheet';
import { saveCourierAction } from '@/lib/actions/accounts';

/**
 * Fiche livreur — DOMAIN.md §1.6.
 *
 * Le RIB vit ICI, sur la fiche, et non sur le compte de connexion : le legacy
 * les confondait sur `users`, si bien qu'un livreur sans accès web n'avait pas
 * où poser ses coordonnées bancaires.
 *
 * Comme pour les e-commerçants, le formulaire attend un `CourierDetail` : la
 * ligne de liste ne porte ni la CNI, ni le RIB, ni le fixe mensuel.
 */
export function CourierForm({
  courier,
  cities,
  agencies,
  onDone,
}: {
  courier: CourierDetail | null;
  cities: City[];
  agencies: Agency[];
  onDone: () => void;
}) {
  const t = useTranslations('accounts.couriers');
  const tVehicle = useTranslations('accounts.couriers.vehicles');
  const tModel = useTranslations('accounts.couriers.payModels');
  const tPeriod = useTranslations('accounts.couriers.payPeriods');
  const tSettlement = useTranslations('accounts.couriers.settlementModes');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);

  const form = useForm<CourierInput, unknown, CourierPayload>({
    resolver: zodResolver(courierSchema),
    defaultValues: {
      firstName: courier?.firstName ?? '',
      lastName: courier?.lastName ?? '',
      phone: courier?.phone ?? '',
      nationalId: courier?.nationalId ?? '',
      cityId: courier?.cityId ?? '',
      agencyId: courier?.agencyId ?? '',
      vehicleType: courier?.vehicleType ?? 'motorbike',
      bankName: courier?.bankName ?? '',
      rib: courier?.rib ?? '',
      payModel: courier?.payModel ?? 'commission',
      settlementMode: courier?.settlementMode ?? 'at-remittance',
      payPeriod: courier?.payPeriod ?? 'monthly',
      // Les montants se LISENT en centimes et se SAISISSENT en dirhams.
      fixedSalary: courier?.fixedSalary == null ? null : courier.fixedSalary / 100,
      isActive: courier?.isActive ?? true,
    },
    mode: 'onBlur',
  });

  const payModel = form.watch('payModel');

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await saveCourierAction(courier?.id ?? null, values);
    setSaving(false);

    if (result.ok) {
      const name = `${values.firstName} ${values.lastName}`;
      toast.success(courier ? t('updated', { name }) : t('created', { name }));
      onDone();
      return;
    }

    if (result.field === 'phone') form.setError('phone', { message: result.error });
    else if (result.field === 'rib') form.setError('rib', { message: result.error });
    else toast.error(result.error === 'invalid' ? tError('invalid') : result.error);
  });

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={courier ? t('edit') : t('new')}
      description={t('formHint')}
      onSubmit={submit}
      submitting={saving}
    >
      <Form {...form}>
        <form className="flex flex-col gap-4" onSubmit={submit}>
          <div className="grid grid-cols-2 gap-3">
            <FormField
              control={form.control}
              name="firstName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.firstName')}</FormLabel>
                  <FormControl>
                    <Input {...field} autoFocus />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="lastName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.lastName')}</FormLabel>
                  <FormControl>
                    <Input {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

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
              name="nationalId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.nationalId')}</FormLabel>
                  <FormControl>
                    <Input {...field} />
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
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <FormField
            control={form.control}
            name="vehicleType"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.vehicleType')}</FormLabel>
                <Select value={field.value} onValueChange={field.onChange}>
                  <FormControl>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                  </FormControl>
                  <SelectContent>
                    {VEHICLE_TYPES.map((type) => (
                      <SelectItem key={type} value={type}>
                        {tVehicle(type)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FormMessage />
              </FormItem>
            )}
          />

          <div className="grid grid-cols-2 gap-3">
            <FormField
              control={form.control}
              name="bankName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.bankName')}</FormLabel>
                  <FormControl>
                    <Input {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="rib"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.rib')}</FormLabel>
                  <FormControl>
                    <Input {...field} inputMode="numeric" maxLength={24} />
                  </FormControl>
                  <FormDescription>{t('fields.ribHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <FormField
              control={form.control}
              name="payModel"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.payModel')}</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {PAY_MODELS.map((model) => (
                        <SelectItem key={model} value={model}>
                          {tModel(model)}
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
              name="settlementMode"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.settlementMode')}</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {SETTLEMENT_MODES.map((mode) => (
                        <SelectItem key={mode} value={mode}>
                          {tSettlement(mode)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormDescription>{t('fields.settlementModeHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="payPeriod"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.payPeriod')}</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {PAY_PERIODS.map((period) => (
                        <SelectItem key={period} value={period}>
                          {tPeriod(period)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          {/* Le fixe ne concerne que les modes « fixe » et « mixte » (D-020) :
              le demander à un livreur payé à la seule commission n'aurait pas
              de sens. Le mode de RÈGLEMENT, lui, est demandé dans tous les cas :
              c'est l'autre axe, et il vaut aussi pour un salarié (D-058). */}
          {payModel === 'salary' || payModel === 'hybrid' ? (
            <FormField
              control={form.control}
              name="fixedSalary"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.fixedSalary')}</FormLabel>
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
                  <FormDescription>{t('fields.fixedSalaryHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          ) : null}

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
