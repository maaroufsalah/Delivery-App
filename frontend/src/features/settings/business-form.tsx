'use client';

import {
  type BusinessSettings,
  type BusinessSettingsInput,
  type BusinessSettingsPayload,
  businessSettingsSchema,
  LABEL_TEMPLATES,
  LOCALES,
  type AppLocale,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { updateBusinessSettingsAction } from '@/lib/actions/accounts';

/**
 * Réglages métier — D-019, « paramétrable avant codé en dur ».
 *
 * Ce qui reste ici après le regroupement : le préfixe des codes colis et les
 * langues de l'installation. Les quatre montants qui vivaient au-dessus —
 * ramassage, seuil de gratuité, frais d'échec — sont partis dans
 * **Paramètres > Finance**, avec les autres politiques d'argent. Ajuster un
 * tarif et activer une langue ne sont pas le même geste.
 */
export function BusinessForm({ settings }: { settings: BusinessSettings }) {
  const t = useTranslations('settings.business');
  const tLocale = useTranslations('locale');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);

  const form = useForm<BusinessSettingsInput, unknown, BusinessSettingsPayload>({
    resolver: zodResolver(businessSettingsSchema),
    defaultValues: {
      parcelCodePrefix: settings.parcelCodePrefix,
      localesEnabled: settings.localesEnabled as AppLocale[],
      localesDefault: settings.localesDefault as AppLocale,
      claimResolutionDays: settings.claimResolutionDays,
      /* L'étiquette — chantier « Étiquette orange », point 1 (D-019). */
      labelTemplate: settings.labelTemplate,
      labelBrandColor: settings.labelBrandColor,
      labelBanner: settings.labelBanner,
    },
    mode: 'onBlur',
  });

  const enabled = (form.watch('localesEnabled') ?? []) as AppLocale[];

  const toggleLocale = (locale: AppLocale, checked: boolean) => {
    const next = checked
      ? [...new Set([...enabled, locale])]
      : enabled.filter((entry) => entry !== locale);

    form.setValue('localesEnabled', next, { shouldDirty: true, shouldValidate: true });

    // Retirer la langue par défaut laisserait une installation sans langue
    // servable : elle bascule sur la première encore active.
    if (!next.includes(form.getValues('localesDefault') as AppLocale) && next[0]) {
      form.setValue('localesDefault', next[0], { shouldDirty: true });
    }
  };

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await updateBusinessSettingsAction(values);
    setSaving(false);

    if (result.ok) {
      toast.success(t('saved'));
      return;
    }

    toast.error(result.error === 'invalid' ? tError('invalid') : result.error);
  });

  return (
    <Form {...form}>
      <form className="flex flex-col gap-4" onSubmit={submit}>
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('parcels')}</CardTitle>
          </CardHeader>
          <CardContent>
            <FormField
              control={form.control}
              name="parcelCodePrefix"
              render={({ field }) => (
                <FormItem className="max-w-xs">
                  <FormLabel>{t('parcelCodePrefix')}</FormLabel>
                  <FormControl>
                    <Input {...field} maxLength={6} className="font-mono uppercase" />
                  </FormControl>
                  <FormDescription>{t('parcelCodePrefixHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        {/* Le délai de traitement des réclamations est une politique de
            SERVICE : il se règle avec les autres réglages d'installation, pas
            au milieu du référentiel des motifs (C7 bloc 2). */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('claims')}</CardTitle>
          </CardHeader>
          <CardContent>
            <FormField
              control={form.control}
              name="claimResolutionDays"
              render={({ field }) => (
                <FormItem className="max-w-xs">
                  <FormLabel>{t('claimResolutionDays')}</FormLabel>
                  <FormControl>
                    {/* `value` est typé `unknown` : le schéma coerce la
                        chaîne du champ en entier, et `z.input` ne peut donc
                        pas s'engager sur le type saisi. */}
                    <Input
                      {...field}
                      value={String(field.value ?? 0)}
                      type="number"
                      min={0}
                      max={365}
                      inputMode="numeric"
                      className="tabular-nums"
                    />
                  </FormControl>
                  <FormDescription>{t('claimResolutionDaysHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        {/*
          L'ÉTIQUETTE — chantier « Étiquette orange », point 1.

          « Gabarit paramétré par tenant (couleurs, logo, bandeau) : wimo garde
          son rendu actuel. » C'est cette carte qui rend la phrase vraie : le
          nouveau gabarit ne s'impose à personne, une société le choisit.

          Le LOGO n'est pas ici : c'est celui de Paramètres > Société, et le
          redemander aurait fait deux logos à tenir à jour.
        */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('labels')}</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-2">
            <FormField
              control={form.control}
              name="labelTemplate"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('labelTemplate')}</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger className="w-full">
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {LABEL_TEMPLATES.map((template) => (
                        <SelectItem key={template} value={template}>
                          {t(`labelTemplates.${template}`)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormDescription>{t('labelTemplateHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="labelBrandColor"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('labelBrandColor')}</FormLabel>
                  <FormControl>
                    <Input
                      {...field}
                      value={String(field.value ?? '')}
                      placeholder="#F7941E"
                      maxLength={9}
                      className="font-mono"
                    />
                  </FormControl>
                  <FormDescription>{t('labelBrandColorHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="labelBanner"
              render={({ field }) => (
                <FormItem className="sm:col-span-2">
                  <FormLabel>{t('labelBanner')}</FormLabel>
                  <FormControl>
                    <Input {...field} value={String(field.value ?? '')} maxLength={200} />
                  </FormControl>
                  <FormDescription>{t('labelBannerHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('locales')}</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-2">
            <FormField
              control={form.control}
              name="localesEnabled"
              render={() => (
                <FormItem>
                  <FormLabel>{t('localesEnabled')}</FormLabel>
                  <div className="flex flex-col gap-1 rounded-lg border p-2">
                    {LOCALES.map((locale) => (
                      <label
                        key={locale}
                        className="hover:bg-accent/60 flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm"
                      >
                        <Checkbox
                          checked={enabled.includes(locale)}
                          // La dernière langue active ne se décoche pas : une
                          // installation sans langue n'a rien à servir.
                          disabled={enabled.length === 1 && enabled.includes(locale)}
                          onCheckedChange={(checked) => toggleLocale(locale, checked === true)}
                        />
                        <span>{tLocale(locale)}</span>
                      </label>
                    ))}
                  </div>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="localesDefault"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('localesDefault')}</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {enabled.map((locale) => (
                        <SelectItem key={locale} value={locale}>
                          {tLocale(locale)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormDescription>{t('localesHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        <Button type="submit" disabled={saving} className="gap-1.5 self-start">
          {saving ? <Loader2 className="size-4 animate-spin" /> : null}
          {t('save')}
        </Button>
      </form>
    </Form>
  );
}
