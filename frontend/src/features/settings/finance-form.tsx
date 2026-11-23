'use client';

import {
  type BankAccount,
  type FinanceSettings,
  type FinanceSettingsInput,
  type FinanceSettingsPayload,
  financeSettingsSchema,
  PAY_PERIODS,
  PRO_VAT_RATES,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { SearchableSelect } from '@/components/searchable-select';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
import { updateFinanceSettingsAction } from '@/lib/actions/settings';

/** Aucun compte choisi : `Select` n'accepte pas la chaîne vide comme valeur. */
const NO_ACCOUNT = '__none__';

/**
 * Paramètres > Finance — D-019, poussé jusqu'au bout.
 *
 * Quatre blocs, dans l'ordre où on les règle : la TVA et la numérotation une
 * fois pour toutes à l'installation, les tarifs quand la politique commerciale
 * bouge, la rémunération livreur quand on embauche, la facture quand le
 * comptable réclame ses mentions.
 *
 * Les montants se saisissent en DIRHAMS et la TVA en POURCENT ; la conversion
 * en centimes et en centièmes se fait au bord, côté API. Aucun flottant ne
 * descend plus bas.
 */
export function FinanceForm({
  settings,
  accounts,
}: {
  settings: FinanceSettings;
  accounts: BankAccount[];
}) {
  const t = useTranslations('settings.finance');
  const tPeriod = useTranslations('settings.finance.period');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);

  const form = useForm<FinanceSettingsInput, unknown, FinanceSettingsPayload>({
    resolver: zodResolver(financeSettingsSchema),
    defaultValues: {
      /*
       * PLUS DE DIVISION ICI — B-14, fermé par la clôture V1.
       *
       * `GET /settings/finance` rend désormais la même unité que `PATCH`
       * attend : des dirhams et des pourcents. Le formulaire divisait pour
       * compenser un contrat asymétrique ; le contrat ne l'est plus, et
       * diviser encore afficherait 0,20 % pour une TVA à 20 %.
       */
      vatRate: settings.vatRate,
      proVatRate: settings.proVatRate,
      invoicePrefix: settings.invoicePrefix,
      remittancePrefix: settings.remittancePrefix,
      statementPrefix: settings.statementPrefix,
      codPayoutPrefix: settings.codPayoutPrefix,
      numberingYearlyReset: settings.numberingYearlyReset,
      failureFeeSameCity: settings.failureFeeSameCity,
      failureFeeCrossCity: settings.failureFeeCrossCity,
      pickupFee: settings.pickupFee,
      freePickupThreshold: settings.freePickupThreshold,
      courierReturnFee: settings.courierReturnFee,
      courierFailedCommission: settings.courierFailedCommission,
      courierPayPeriod: settings.courierPayPeriod,
      invoiceFooter: settings.invoiceFooter,
      codPayoutFooter: settings.codPayoutFooter,
      defaultBankAccountId: settings.defaultBankAccountId ?? '',
      auditRetentionDays: settings.auditRetentionDays,
    },
    mode: 'onBlur',
  });

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await updateFinanceSettingsAction(values);
    setSaving(false);

    if (result.ok) {
      toast.success(t('saved'));
      return;
    }

    toast.error(result.error === 'invalid' ? tError('invalid') : result.error);
  });

  const amountField = (
    name:
      | 'failureFeeSameCity'
      | 'failureFeeCrossCity'
      | 'pickupFee'
      | 'courierReturnFee'
      | 'courierFailedCommission'
      | 'vatRate',
    hint?: string,
  ) => (
    <FormField
      control={form.control}
      name={name}
      render={({ field }) => (
        <FormItem>
          <FormLabel>{t(name)}</FormLabel>
          <FormControl>
            <Input
              type="number"
              min={0}
              step="0.01"
              value={field.value}
              onChange={(event) => field.onChange(event.target.valueAsNumber)}
            />
          </FormControl>
          {hint ? <FormDescription>{hint}</FormDescription> : null}
          <FormMessage />
        </FormItem>
      )}
    />
  );

  const prefixField = (
    name: 'invoicePrefix' | 'remittancePrefix' | 'statementPrefix' | 'codPayoutPrefix',
  ) => (
    <FormField
      control={form.control}
      name={name}
      render={({ field }) => (
        <FormItem>
          <FormLabel>{t(name)}</FormLabel>
          <FormControl>
            <Input {...field} maxLength={5} className="font-mono uppercase" />
          </FormControl>
          <FormMessage />
        </FormItem>
      )}
    />
  );

  return (
    <Form {...form}>
      <form className="flex flex-col gap-4" onSubmit={submit}>
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('numbering')}</CardTitle>
            <CardDescription>{t('numberingHint')}</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-3">
            {prefixField('invoicePrefix')}
            {prefixField('remittancePrefix')}
            {prefixField('statementPrefix')}
            {/* C11 bloc 3 — le bon de versement a son propre compteur. */}
            {prefixField('codPayoutPrefix')}

            <FormField
              control={form.control}
              name="numberingYearlyReset"
              render={({ field }) => (
                <FormItem className="sm:col-span-3">
                  <div className="flex items-center gap-3">
                    <FormControl>
                      <Switch checked={field.value} onCheckedChange={field.onChange} />
                    </FormControl>
                    <FormLabel className="font-normal">{t('numberingYearlyReset')}</FormLabel>
                  </div>
                  {/* Le choix ne se rattrape pas : une série déjà émise garde
                      sa convention, la bascule ouvre la suivante. */}
                  <FormDescription>{t('numberingYearlyResetHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('pricing')}</CardTitle>
            <CardDescription>{t('pricingHint')}</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-2">
            {amountField('failureFeeSameCity', t('failureFeeHint'))}
            {amountField('failureFeeCrossCity')}
            {amountField('pickupFee', t('pickupFeeHint'))}

            <FormField
              control={form.control}
              name="freePickupThreshold"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('freePickupThreshold')}</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      min={0}
                      max={1000}
                      step={1}
                      value={field.value}
                      onChange={(event) => field.onChange(event.target.valueAsNumber)}
                    />
                  </FormControl>
                  <FormDescription>{t('freePickupThresholdHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('courierPay')}</CardTitle>
            <CardDescription>{t('courierPayHint')}</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-2">
            {amountField('courierReturnFee', t('courierReturnFeeHint'))}
            {amountField('courierFailedCommission', t('courierFailedCommissionHint'))}

            <FormField
              control={form.control}
              name="courierPayPeriod"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('courierPayPeriod')}</FormLabel>
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
                  <FormDescription>{t('courierPayPeriodHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('invoice')}</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-2">
            {/*
              LOT R (D-230) — la TVA des Clients Pro : un défaut société, en
              pourcent, parmi les taux marocains ; chaque fiche Pro peut le
              remplacer par le sien. Le champ voisin ne vaut plus que pour les
              factures des clients non pro, inchangées.
            */}
            <FormField
              control={form.control}
              name="proVatRate"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('proVatRate')}</FormLabel>
                  <Select
                    value={String(field.value ?? 20)}
                    onValueChange={(value) => field.onChange(Number(value))}
                  >
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {PRO_VAT_RATES.map((rate) => (
                        <SelectItem key={rate} value={String(rate / 100)}>
                          {rate === 0
                            ? t('proVatExempt')
                            : t('proVatPercent', { rate: String(rate / 100) })}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormDescription>{t('proVatRateHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            {amountField('vatRate', t('vatRateHint'))}

            <FormField
              control={form.control}
              name="defaultBankAccountId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('defaultBankAccountId')}</FormLabel>
                  <FormControl>
                    <SearchableSelect
                      options={accounts.map((account) => ({
                        value: account.id,
                        label: `${account.label}, ${account.bankName}`,
                        keywords: [account.bankName],
                      }))}
                      /* B-21 : le champ accepte désormais le `null` que le `GET`
                         renvoie — le sélecteur les traite comme « aucun compte ». */
                      value={field.value ? field.value : NO_ACCOUNT}
                      onValueChange={(value) => field.onChange(value === NO_ACCOUNT ? '' : value)}
                      emptyOption={{ value: NO_ACCOUNT, label: t('noAccount') }}
                    />
                  </FormControl>
                  <FormDescription>{t('defaultBankAccountHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="invoiceFooter"
              render={({ field }) => (
                <FormItem className="sm:col-span-2">
                  <FormLabel>{t('invoiceFooter')}</FormLabel>
                  <FormControl>
                    <Textarea {...field} rows={3} maxLength={300} />
                  </FormControl>
                  <FormDescription>{t('invoiceFooterHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="codPayoutFooter"
              render={({ field }) => (
                <FormItem className="sm:col-span-2">
                  <FormLabel>{t('codPayoutFooter')}</FormLabel>
                  <FormControl>
                    <Textarea {...field} rows={3} maxLength={300} />
                  </FormControl>
                  {/*
                    Séparé de la facture : les deux documents ne disent pas la
                    même chose. Réutiliser la mention de facture ferait imprimer
                    des conditions de règlement sur un document qui en CONSTATE un.
                  */}
                  <FormDescription>{t('codPayoutFooterHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        {/* C11 bloc 8 — ce qu'on GARDE, et combien de temps. */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('retention')}</CardTitle>
            <CardDescription>{t('retentionHint')}</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-2">
            <FormField
              control={form.control}
              name="auditRetentionDays"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('auditRetentionDays')}</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      min={0}
                      step="1"
                      value={field.value}
                      onChange={(event) => field.onChange(event.target.valueAsNumber)}
                    />
                  </FormControl>
                  {/* Zéro = illimité, et c'est le défaut : effacer un journal
                      d'audit est irréversible (D-064). */}
                  <FormDescription>{t('auditRetentionDaysHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            {/*
              LECTURE SEULE — D-248 : aucune pièce jointe ne se supprime avec
              l'âge. On l'affiche pour qu'un administrateur sache que ses
              images de chèques et justificatifs restent.
            */}
            <FormItem>
              <FormLabel>{t('uploadsRetentionDays')}</FormLabel>
              <Input
                readOnly
                disabled
                value={
                  settings.uploadsRetentionDays === 0
                    ? t('unlimited')
                    : t('days', { count: String(settings.uploadsRetentionDays) })
                }
              />
              <FormDescription>{t('uploadsRetentionDaysHint')}</FormDescription>
            </FormItem>
          </CardContent>
        </Card>

        <div className="flex justify-end">
          <Button type="submit" disabled={saving} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : null}
            {t('save')}
          </Button>
        </div>
      </form>
    </Form>
  );
}
