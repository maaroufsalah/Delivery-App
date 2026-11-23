'use client';

import {
  type City,
  type MerchantCompany,
  type MerchantCompanyInput,
  merchantCompanySchema,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useTransition } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { CitySelect } from '@/components/city-select';
import { updateMerchantCompanyAction } from '@/lib/actions/merchant';

/**
 * « MA SOCIÉTÉ » — V2 vendeur bloc 9, spécification §7.
 *
 * **Ce que l'écran répare.** Ces champs n'étaient éditables que par
 * l'administration : un vendeur qui déménageait devait téléphoner à son
 * agence, et ses bons de ramassage partaient à l'ancienne adresse en
 * attendant.
 *
 * **Quatre champs, et pas un de plus.** Le régime de facturation, le compte
 * bancaire et l'agence de rattachement restent à l'administration : ce sont des
 * termes commerciaux négociés, pas des coordonnées. Les ouvrir laisserait un
 * vendeur passer seul en « professionnel », c'est-à-dire changer la façon dont
 * son argent lui revient (D-061).
 *
 * **L'e-mail est affiché, pas modifiable.** Il identifie le COMPTE autant que
 * la fiche, et le changer demande de vérifier la nouvelle adresse — c'est le
 * travail du formulaire juste au-dessus, pas de celui-ci.
 */
export function MerchantCompanyForm({
  company,
  cities,
}: {
  company: MerchantCompany;
  cities: City[];
}) {
  const t = useTranslations('merchantCompany');
  const format = useFormatter();
  const [pending, startTransition] = useTransition();

  const form = useForm<MerchantCompanyInput>({
    resolver: zodResolver(merchantCompanySchema),
    defaultValues: {
      name: company.name,
      phone: company.phone,
      cityId: company.cityId,
      address: company.address ?? '',
    },
  });

  const onSubmit = (values: MerchantCompanyInput) =>
    startTransition(async () => {
      const result = await updateMerchantCompanyAction(values);

      if (!result.ok) {
        toast.error(result.error);
        return;
      }

      toast.success(t('saved'));
    });

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('title')}</CardTitle>
        <CardDescription>{t('description')}</CardDescription>
      </CardHeader>

      <CardContent>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-4">
            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('name')}</FormLabel>
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
                name="phone"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('phone')}</FormLabel>
                    <FormControl>
                      <Input {...field} type="tel" dir="ltr" />
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
                    <FormLabel>{t('city')}</FormLabel>
                    <FormControl>
                      <CitySelect
                        cities={cities}
                        value={field.value}
                        onValueChange={field.onChange}
                        placeholder={t('city')}
                      />
                    </FormControl>
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
                  <FormLabel>{t('address')}</FormLabel>
                  <FormControl>
                    <Input {...field} value={field.value ?? ''} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            {/* L'e-mail se lit, il ne se change pas ici. */}
            <div className="flex flex-col gap-1">
              <span className="text-sm font-medium">{t('email')}</span>
              <span className="text-muted-foreground text-sm" dir="ltr">
                {company.email}
              </span>
            </div>

            {/*
              SES CONDITIONS DE RAMASSAGE — lot T (D-232). En lecture : c'est
              l'administration qui les fixe, et aucune route vendeur ne les écrit.
            */}
            <div className="flex flex-col gap-1">
              <span className="text-sm font-medium">{t('pickupTerms')}</span>
              <span className="text-muted-foreground text-sm">
                {company.pickupTerms.fee === 0 || company.pickupTerms.freeFrom === 0
                  ? t('pickupTermsFree')
                  : t('pickupTermsValue', {
                      fee: format.number(company.pickupTerms.fee / 100, 'currency'),
                      threshold: String(company.pickupTerms.freeFrom),
                    })}
              </span>
            </div>

            <Button type="submit" size="sm" className="gap-1.5 self-start" disabled={pending}>
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('save')}
            </Button>
          </form>
        </Form>
      </CardContent>
    </Card>
  );
}
