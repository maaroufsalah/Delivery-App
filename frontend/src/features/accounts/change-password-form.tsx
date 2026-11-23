'use client';

import { PASSWORD_MIN_LENGTH, changePasswordSchema } from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
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
import { useRouter } from '@/i18n/navigation';
import { changeOwnPasswordAction } from '@/lib/actions/accounts';

type PasswordInput = {
  currentPassword: string;
  password: string;
  confirmation: string;
};

/**
 * Changement de mot de passe par le titulaire.
 *
 * L'ancien mot de passe est demandé MÊME après une invitation : un poste resté
 * ouvert ne doit pas suffire à s'approprier le compte. C'est aussi la seule
 * route que l'API laisse ouverte à un compte encore sous mot de passe
 * temporaire.
 *
 * `router.refresh()` après succès : le profil porte `mustChangePassword`, et
 * c'est lui qui décide si le back-office s'ouvre.
 */
export function ChangePasswordForm() {
  const t = useTranslations('accounts.changePassword');
  const tError = useTranslations('validation');
  const router = useRouter();
  const [saving, setSaving] = useState(false);

  const form = useForm<PasswordInput>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: '', password: '', confirmation: '' },
    mode: 'onBlur',
  });

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await changeOwnPasswordAction(values);
    setSaving(false);

    if (result.ok) {
      form.reset();
      toast.success(t('done'));
      router.refresh();
      return;
    }

    if (result.field === 'currentPassword') {
      form.setError('currentPassword', { message: result.error });
      return;
    }

    toast.error(result.error === 'invalid' ? tError('invalid') : result.error);
  });

  return (
    <Form {...form}>
      <form className="flex max-w-md flex-col gap-4" onSubmit={submit}>
        <FormField
          control={form.control}
          name="currentPassword"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('current')}</FormLabel>
              <FormControl>
                <Input {...field} type="password" autoComplete="current-password" />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="password"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('password')}</FormLabel>
              <FormControl>
                <Input {...field} type="password" autoComplete="new-password" />
              </FormControl>
              <FormDescription>{t('hint', { min: PASSWORD_MIN_LENGTH })}</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="confirmation"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('confirmation')}</FormLabel>
              <FormControl>
                <Input {...field} type="password" autoComplete="new-password" />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <Button type="submit" disabled={saving} className="gap-1.5 self-start">
          {saving ? <Loader2 className="size-4 animate-spin" /> : null}
          {t('submit')}
        </Button>
      </form>
    </Form>
  );
}
