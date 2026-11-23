'use client';

import { type UpdateOwnProfileInput, updateOwnProfileSchema } from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { type Resolver, useForm } from 'react-hook-form';
import { toast } from 'sonner';
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
import { updateOwnProfileAction } from '@/lib/actions/accounts';

/**
 * « Mon profil » — C13 bloc 4, pour TOUS les rôles.
 *
 * Quatre champs. Ni rôles, ni agences, ni activation : ce sont des décisions
 * que quelqu'un d'autre prend, et les afficher grisés inviterait à les demander
 * plutôt qu'à les oublier.
 *
 * L'adresse est modifiable par son titulaire. C'est son identifiant de
 * connexion ; lui faire réclamer à un administrateur le changement d'une
 * adresse qui n'appartient qu'à lui serait une politesse inutile (D-086).
 * Changer d'adresse ne déconnecte pas : la session tient à l'identifiant du
 * compte, pas à son login.
 */
export function ProfileForm({
  profile,
}: {
  /** `email` nul : livreur connecté par son nom d'utilisateur (point 5). */
  profile: { firstName: string; lastName: string; phone: string | null; email: string | null };
}) {
  const t = useTranslations('accounts.profile');
  const tCommon = useTranslations('common');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);

  const form = useForm<UpdateOwnProfileInput>({
    // Le schéma rend `email: null` pour un champ vide (point 5) : sa sortie
    // n'est plus son entrée, d'où le résolveur typé sur l'entrée du formulaire.
    resolver: zodResolver(updateOwnProfileSchema) as unknown as Resolver<UpdateOwnProfileInput>,
    mode: 'onBlur',
    defaultValues: {
      firstName: profile.firstName,
      lastName: profile.lastName,
      phone: profile.phone ?? '',
      email: profile.email ?? '',
    },
  });

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await updateOwnProfileAction(values);
    setSaving(false);

    if (result.ok) {
      toast.success(t('saved'));
      return;
    }

    if (result.field === 'email') form.setError('email', { message: result.error });
    else toast.error(result.error === 'invalid' ? tError('invalid') : result.error);
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{t('title')}</CardTitle>
        <CardDescription>{t('hint')}</CardDescription>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form className="flex flex-col gap-4" onSubmit={submit}>
            <div className="grid gap-4 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="firstName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('firstName')}</FormLabel>
                    <FormControl>
                      <Input {...field} autoFocus autoComplete="given-name" />
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
                    <FormLabel>{t('lastName')}</FormLabel>
                    <FormControl>
                      <Input {...field} autoComplete="family-name" />
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
                      <Input {...field} type="tel" inputMode="tel" dir="ltr" autoComplete="tel" />
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
                      <Input
                        {...field}
                        value={field.value ?? ''}
                        type="email"
                        dir="ltr"
                        autoComplete="username"
                        onChange={(event) => field.onChange(event.target.value.toLowerCase())}
                      />
                    </FormControl>
                    <FormDescription>{t('emailHint')}</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <div className="flex justify-end">
              <Button type="submit" disabled={saving} className="gap-1.5">
                {saving ? <Loader2 className="size-4 animate-spin" aria-hidden /> : null}
                {tCommon('save')}
              </Button>
            </div>
          </form>
        </Form>
      </CardContent>
    </Card>
  );
}
