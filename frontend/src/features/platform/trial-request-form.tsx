'use client';

import { type TrialRequestInput, trialRequestSchema } from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { CheckCircle2, Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { useForm } from 'react-hook-form';
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
import { Textarea } from '@/components/ui/textarea';
import { requestTrialAction } from '@/lib/actions/platform-public';

/**
 * « Demander un essai » — C8 bloc 5.
 *
 * Il n'inscrit personne : il dépose une demande que la console affiche, et un
 * humain rappelle. C'est écrit dans la description, parce qu'un formulaire qui
 * ressemble à une inscription et n'en est pas une laisse le visiteur attendre
 * un e-mail qui ne viendra pas.
 *
 * **L'adresse de contact est visible même en cas d'échec.** Un formulaire
 * public finit toujours par tomber — API en maintenance, débit limité atteint —
 * et un prospect perdu sur une erreur est un prospect perdu tout court.
 */
export function TrialRequestForm({ contactEmail }: { contactEmail: string }) {
  const t = useTranslations('platform.landing.form');
  const tLanding = useTranslations('platform.landing');
  const [pending, startTransition] = useTransition();
  const [sent, setSent] = useState(false);
  const [failed, setFailed] = useState(false);

  const form = useForm<TrialRequestInput>({
    resolver: zodResolver(trialRequestSchema),
    defaultValues: { company: '', contactName: '', email: '', phone: '', message: '' },
    mode: 'onBlur',
  });

  const onSubmit = (values: TrialRequestInput) => {
    setFailed(false);

    startTransition(async () => {
      const result = await requestTrialAction(values);

      if (!result.ok) {
        setFailed(true);
        return;
      }

      setSent(true);
    });
  };

  if (sent) {
    return (
      <Card>
        <CardContent className="flex flex-col items-center gap-3 py-14 text-center">
          <CheckCircle2 className="size-8 text-emerald-600 dark:text-emerald-400" aria-hidden />
          <p className="text-sm font-semibold">{t('doneTitle')}</p>
          <p className="text-muted-foreground text-sm">{t('doneBody')}</p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('title')}</CardTitle>
        <CardDescription>{t('description')}</CardDescription>
      </CardHeader>

      <CardContent>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="company"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('company')}</FormLabel>
                    <FormControl>
                      <Input {...field} autoComplete="organization" />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="contactName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('contactName')}</FormLabel>
                    <FormControl>
                      <Input {...field} autoComplete="name" />
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
                    <FormLabel>{t('email')}</FormLabel>
                    <FormControl>
                      <Input {...field} type="email" dir="ltr" autoComplete="email" />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="phone"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('phone')}</FormLabel>
                    <FormControl>
                      <Input
                        {...field}
                        value={field.value ?? ''}
                        type="tel"
                        dir="ltr"
                        autoComplete="tel"
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="message"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('message')}</FormLabel>
                  <FormControl>
                    <Textarea {...field} value={field.value ?? ''} rows={3} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            {failed ? (
              <p role="alert" className="text-destructive text-sm">
                {t('failed', { email: contactEmail })}
              </p>
            ) : null}

            <div className="flex flex-wrap items-center justify-between gap-3">
              <p className="text-muted-foreground text-xs">
                {tLanding('contact', { email: contactEmail })}
              </p>

              <Button type="submit" disabled={pending} className="gap-1.5">
                {pending ? <Loader2 className="size-4 animate-spin" /> : null}
                {t('submit')}
              </Button>
            </div>
          </form>
        </Form>
      </CardContent>
    </Card>
  );
}
