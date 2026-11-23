'use client';

import {
  type City,
  PARCEL_QUANTITY_MAX,
  type ParcelDetail,
  type SellerParcelUpdateInput,
  sellerParcelUpdateSchema,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2, Pencil, Trash2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { type Resolver, useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { CitySelect } from '@/components/city-select';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
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
import { ExpectedPaymentField } from '@/features/parcels/expected-payment-field';
import { useRouter } from '@/i18n/navigation';
import type { ReferentialResult } from '@/lib/actions/action-result';
import { deleteSellerParcelAction, updateSellerParcelAction } from '@/lib/actions/parcels';

/**
 * LE VENDEUR CORRIGE OU SUPPRIME SON COLIS — recette SpeedyTrans 3, point 3.
 *
 * Deux boutons sur SA fiche colis, tant que le colis n'est pas ramassé (la
 * page ne les rend pas sinon, et l'API rendrait 422 de toute façon). La
 * modification reprend les champs de la saisie ; changer de ville recalcule
 * les frais côté serveur, exactement comme à la création.
 */
export function SellerParcelActions({
  parcel,
  cities,
  isPro,
}: {
  parcel: ParcelDetail;
  cities: City[];
  /** Le vendeur est Client Pro : BL et paiement attendu se posent aussi ici. */
  isPro: boolean;
}) {
  const t = useTranslations('parcels.sellerEdit');
  const tForm = useTranslations('parcels.form');
  const tCommon = useTranslations('common');
  const router = useRouter();
  const [editing, setEditing] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [pending, startTransition] = useTransition();
  // Lot G (D-208) : la case est cochée d'office pour un colis déjà en plusieurs cartons.
  const [multiCarton, setMultiCarton] = useState((parcel.quantity ?? 1) > 1);

  const form = useForm<SellerParcelUpdateInput>({
    resolver: zodResolver(sellerParcelUpdateSchema) as unknown as Resolver<SellerParcelUpdateInput>,
    mode: 'onBlur',
    defaultValues: {
      description: parcel.description ?? '',
      recipientName: parcel.recipientName,
      recipientPhone: parcel.recipientPhone,
      destinationCityId: parcel.destinationCityId,
      destinationDistrictName: parcel.destinationDistrictName ?? '',
      deliveryAddress: parcel.deliveryAddress,
      codAmount: parcel.codAmount / 100,
      blRequested: parcel.blRequested,
      expectedPayment: parcel.expectedPayment ?? 'cash',
      quantity: parcel.quantity ?? 1,
    },
  });

  const message = (result: Extract<ReferentialResult<unknown>, { ok: false }>) =>
    result.reason &&
    ['parcel-picked-up', 'parcel-in-route', 'client-not-pro'].includes(result.reason)
      ? t(result.reason as 'parcel-picked-up')
      : result.error;

  const save = form.handleSubmit((values) => {
    startTransition(async () => {
      const result = await updateSellerParcelAction(parcel.code, {
        ...values,
        // D-264 : les cartons pour tout client ; le BL et le paiement attendu restent Pro.
        quantity: multiCarton ? values.quantity : 1,
        ...(isPro ? {} : { blRequested: undefined, expectedPayment: undefined }),
      });

      if (!result.ok) {
        toast.error(message(result));
        return;
      }

      toast.success(t('saved'));
      setEditing(false);
      router.refresh();
    });
  });

  const remove = () => {
    startTransition(async () => {
      const result = await deleteSellerParcelAction(parcel.code);

      if (!result.ok) {
        toast.error(message(result));
        return;
      }

      toast.success(t('deleted'));
      setDeleting(false);
      router.push('/parcels');
    });
  };

  return (
    <>
      <Button variant="outline" size="sm" className="gap-1.5" onClick={() => setEditing(true)}>
        <Pencil className="size-4" aria-hidden />
        <span className="hidden sm:inline">{t('edit')}</span>
      </Button>
      <Button
        variant="outline"
        size="sm"
        className="text-destructive gap-1.5"
        onClick={() => setDeleting(true)}
      >
        <Trash2 className="size-4" aria-hidden />
        <span className="hidden sm:inline">{t('delete')}</span>
      </Button>

      <Dialog open={editing} onOpenChange={setEditing}>
        <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{t('editTitle', { code: parcel.code })}</DialogTitle>
            <DialogDescription>{t('editHint')}</DialogDescription>
          </DialogHeader>

          <Form {...form}>
            <form onSubmit={save} className="flex flex-col gap-4">
              <FormField
                control={form.control}
                name="recipientName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{tForm('fields.recipientName')}</FormLabel>
                    <FormControl>
                      <Input {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="recipientPhone"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{tForm('fields.recipientPhone')}</FormLabel>
                    <FormControl>
                      <Input {...field} type="tel" inputMode="tel" dir="ltr" />
                    </FormControl>
                    {/* D-244 : un à trois numéros dans le même champ. */}
                    <FormDescription>{tForm('fields.recipientPhoneHint')}</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="destinationCityId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{tForm('fields.destinationCity')}</FormLabel>
                    <FormControl>
                      <CitySelect
                        cities={cities}
                        value={field.value}
                        onValueChange={field.onChange}
                        className="w-full"
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="destinationDistrictName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{tForm('fields.district')}</FormLabel>
                    <FormControl>
                      <Input {...field} value={field.value ?? ''} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="deliveryAddress"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{tForm('fields.deliveryAddress')}</FormLabel>
                    <FormControl>
                      <Input {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <div className="grid gap-4 sm:grid-cols-2">
                <FormField
                  control={form.control}
                  name="codAmount"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{tForm('fields.codAmount')}</FormLabel>
                      <FormControl>
                        <Input
                          type="number"
                          inputMode="decimal"
                          min={0}
                          step="0.01"
                          dir="ltr"
                          value={Number.isFinite(field.value) ? field.value : ''}
                          onChange={(event) => field.onChange(event.target.valueAsNumber)}
                          onBlur={field.onBlur}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="description"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{tForm('fields.description')}</FormLabel>
                      <FormControl>
                        <Input {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>

              {isPro ? (
                <>
                  <FormField
                    control={form.control}
                    name="blRequested"
                    render={({ field }) => (
                      <FormItem className="flex items-center justify-between gap-3 rounded-lg border p-3">
                        <FormLabel className="font-normal">{tForm('fields.blRequested')}</FormLabel>
                        <FormControl>
                          <Switch checked={field.value ?? false} onCheckedChange={field.onChange} />
                        </FormControl>
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={form.control}
                    name="expectedPayment"
                    render={({ field }) => (
                      <FormItem>
                        <FormControl>
                          <ExpectedPaymentField value={field.value} onChange={field.onChange} />
                        </FormControl>
                      </FormItem>
                    )}
                  />
                </>
              ) : null}

              {/* Lot G (D-208), tout client depuis D-264 : changer la quantité recalcule les frais, journalisés. */}
              <label className="flex items-center justify-between gap-3 rounded-lg border p-3 text-sm">
                <span>{tForm('fields.multiCarton')}</span>
                <Switch
                  checked={multiCarton}
                  onCheckedChange={(checked) => {
                    setMultiCarton(checked);
                    form.setValue('quantity', checked ? Math.max(parcel.quantity ?? 1, 2) : 1, {
                      shouldValidate: true,
                    });
                  }}
                />
              </label>
              {multiCarton ? (
                <FormField
                  control={form.control}
                  name="quantity"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{tForm('fields.quantity')}</FormLabel>
                      <FormControl>
                        <Input
                          type="number"
                          inputMode="numeric"
                          min={2}
                          max={PARCEL_QUANTITY_MAX}
                          step={1}
                          dir="ltr"
                          value={field.value === undefined ? '' : String(field.value)}
                          onChange={(event) => field.onChange(event.target.valueAsNumber || '')}
                          onBlur={field.onBlur}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              ) : null}

              <DialogFooter className="gap-2 sm:gap-2">
                <Button type="button" variant="ghost" onClick={() => setEditing(false)}>
                  {tCommon('cancel')}
                </Button>
                <Button type="submit" disabled={pending} className="gap-1.5">
                  {pending ? <Loader2 className="size-4 animate-spin" aria-hidden /> : null}
                  {t('save')}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      <Dialog open={deleting} onOpenChange={setDeleting}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t('deleteTitle', { code: parcel.code })}</DialogTitle>
            <DialogDescription>{t('deleteHint')}</DialogDescription>
          </DialogHeader>
          <DialogFooter className="gap-2 sm:gap-2">
            <Button type="button" variant="ghost" onClick={() => setDeleting(false)}>
              {tCommon('cancel')}
            </Button>
            <Button
              type="button"
              variant="destructive"
              disabled={pending}
              onClick={remove}
              className="gap-1.5"
            >
              {pending ? <Loader2 className="size-4 animate-spin" aria-hidden /> : null}
              {t('delete')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
