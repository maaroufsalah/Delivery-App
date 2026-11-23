'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2, LogIn, UserPlus } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { ForgotPasswordDialog } from '@/features/auth/forgot-password-dialog';
import { Link, useRouter } from '@/i18n/navigation';
import { loginAction } from '@/lib/actions/auth';

/**
 * Connexion réelle.
 *
 * Le formulaire ne vérifie RIEN : il poste à une Server Action, qui appelle
 * l'API et recopie les cookies de session. Aucun jeton ne transite par le
 * JavaScript de la page — ils sont `httpOnly`, donc hors de portée d'un XSS.
 *
 * Le message d'erreur est unique, que l'e-mail soit inconnu ou le mot de passe
 * faux : distinguer les deux cas donnerait la liste des comptes existants.
 */
const loginSchema = z.object({
  /*
   * « E-mail ou nom d'utilisateur » — recette SpeedyTrans 3, point 5. Plus de
   * validation de FORME ici : un nom d'utilisateur n'est pas une adresse, et
   * c'est l'API qui décide lequel des deux on lui donne.
   */
  identifier: z.string().trim().min(1, 'required'),
  password: z.string().min(1, 'required'),
  /**
   * « Se souvenir de moi » — C15 bloc 1.
   *
   * Décoché d'usine, et c'est le bon défaut : un poste d'agence est partagé,
   * et prolonger la session de quelqu'un qui n'a rien demandé serait un choix
   * fait à sa place. Le livreur et le vendeur, eux, cochent une fois.
   */
  remember: z.boolean(),
});

type LoginInput = z.infer<typeof loginSchema>;

export function LoginForm({
  /**
   * La page que l'utilisateur voulait ouvrir quand sa session est tombée —
   * C15 bloc 3. Déjà assainie par la page ; `null` pour une connexion ordinaire.
   */
  next = null,
}: {
  next?: string | null;
}) {
  const t = useTranslations('auth.login');
  const tForgot = useTranslations('auth.forgot');
  const tRegister = useTranslations('auth.register');
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [forgot, setForgot] = useState(false);
  /*
   * Le compte EN ATTENTE ne reçoit pas une erreur de champ — C13 bloc 1.
   *
   * Rien n'a été mal tapé : le mot de passe est bon, c'est la validation qui
   * manque. Une ligne rouge sous le champ ferait recommencer la saisie, encore
   * et encore. Le message vit donc au-dessus du formulaire, et il dit la
   * vérité — la même phrase, mot pour mot, que celle vue à l'inscription.
   */
  const [pendingValidation, setPendingValidation] = useState(false);

  const form = useForm<LoginInput>({
    resolver: zodResolver(loginSchema),
    defaultValues: { identifier: '', password: '', remember: false },
  });

  function onSubmit(values: LoginInput) {
    setPendingValidation(false);

    startTransition(async () => {
      const result = await loginAction(values);

      if (result.ok) {
        /*
         * Le RETOUR à la page demandée, quand il y en a une — C15 bloc 3.
         *
         * `/dashboard` sinon : le layout du back-office redirige ensuite selon
         * le rattachement (D-072), donc un livreur ou un vendeur atterrit chez
         * lui sans que ce formulaire ait à connaître les rôles.
         *
         * `next` porte déjà son préfixe de langue — il vient de l'URL que le
         * proxy a écrite — donc `window.location` plutôt que le routeur
         * localisé, qui le préfixerait une seconde fois.
         */
        if (next) {
          window.location.assign(next);
          return;
        }

        router.push('/dashboard');
        // Les composants serveur doivent relire les cookies fraîchement posés.
        router.refresh();
        return;
      }

      if (result.error === 'pending-validation') {
        setPendingValidation(true);
        return;
      }

      form.setError('password', {
        message:
          result.error === 'unreachable'
            ? 'api-unreachable'
            : result.error === 'login-throttled'
              ? 'login-throttled'
              : 'invalid-credentials',
      });
    });
  }

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-5">
        {pendingValidation ? (
          <p
            role="status"
            className="border-primary/30 bg-primary/5 text-foreground rounded-md border px-3 py-2 text-sm"
          >
            {tRegister('pending')}
          </p>
        ) : null}

        <FormField
          control={form.control}
          name="identifier"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('identifier')}</FormLabel>
              <FormControl>
                {/* `text` et non `email` : le navigateur refuserait un nom. */}
                <Input
                  {...field}
                  type="text"
                  autoComplete="username"
                  autoCapitalize="none"
                  spellCheck={false}
                  dir="ltr"
                />
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
                <Input {...field} type="password" autoComplete="current-password" dir="ltr" />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="remember"
          render={({ field }) => (
            <FormItem className="flex flex-row items-center gap-2 space-y-0">
              <FormControl>
                <Checkbox checked={field.value} onCheckedChange={field.onChange} />
              </FormControl>
              <FormLabel className="cursor-pointer text-sm font-normal">{t('remember')}</FormLabel>
            </FormItem>
          )}
        />

        <Button type="submit" className="gap-1.5" disabled={pending}>
          {pending ? (
            <Loader2 className="size-4 animate-spin" />
          ) : (
            <LogIn className="flip-rtl size-4" aria-hidden />
          )}
          {t('submit')}
        </Button>

        {/* Visible, pas caché — C12 bloc 2b. C'est le geste de quelqu'un qui
            vient d'échouer à se connecter, et le chercher est une humiliation
            de plus. */}
        <button
          type="button"
          className="text-muted-foreground hover:text-foreground focus-visible:ring-ring -mt-1 self-center rounded-md text-xs underline-offset-2 outline-none hover:underline focus-visible:ring-2"
          onClick={() => setForgot(true)}
        >
          {tForgot('link')}
        </button>

        {/* « Créer un compte » — C13 bloc 1. Un BOUTON plein et non un lien
            discret : sur l'espace d'un transporteur, la moitié des visiteurs de
            cette page n'ont pas encore de compte. */}
        <div className="flex items-center gap-3">
          <span className="bg-border h-px flex-1" />
          <span className="text-muted-foreground text-xs">{tRegister('or')}</span>
          <span className="bg-border h-px flex-1" />
        </div>

        <Button asChild variant="outline" className="gap-1.5">
          <Link href="/register">
            <UserPlus className="size-4" aria-hidden />
            {tRegister('cta')}
          </Link>
        </Button>
      </form>

      <ForgotPasswordDialog open={forgot} onOpenChange={setForgot} />
    </Form>
  );
}
