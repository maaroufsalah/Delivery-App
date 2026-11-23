'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import {
  PASSWORD_MIN_LENGTH,
  type PublicCity,
  type RegistrationInput,
  type RegistrationKind,
  registrationSchema,
} from '@delivery/shared';
import { ArrowLeft, Bike, CheckCircle2, Eye, EyeOff, Loader2, Store } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { type Resolver, useForm } from 'react-hook-form';
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
import { CitySelect } from '@/components/city-select';
import { Link } from '@/i18n/navigation';
import { registerAction } from '@/lib/actions/auth';

/**
 * Inscription publique — C13 bloc 1, D-086.
 *
 * TROIS ÉTATS, et pas un écran de plus : le choix du type, le formulaire, la
 * confirmation. La mission demande « deux cartes, pas un select », et la
 * différence n'est pas cosmétique : un select cache ses options derrière un
 * geste, alors que le type de compte est LA décision qui change tout le reste
 * du formulaire. Deux cartes la posent à plat, avec de quoi la comprendre.
 *
 * Le formulaire tient en UN écran, sans étape intermédiaire. Sept champs pour
 * un e-commerçant, six pour un livreur : les découper en assistant ferait trois
 * clics de plus pour la même saisie.
 */

type Step = { name: 'kind' } | { name: 'form'; kind: RegistrationKind } | { name: 'done' };

export function RegisterScreen({ cities }: { cities: PublicCity[] }) {
  const t = useTranslations('auth.register');
  const [step, setStep] = useState<Step>({ name: 'kind' });

  if (step.name === 'done') return <RegistrationDone />;

  if (step.name === 'kind') {
    return <KindChoice onChoose={(kind) => setStep({ name: 'form', kind })} />;
  }

  return (
    <Card className="w-full max-w-lg">
      <CardHeader>
        <button
          type="button"
          onClick={() => setStep({ name: 'kind' })}
          className="text-muted-foreground hover:text-foreground focus-visible:ring-ring -mt-1 mb-1 flex w-fit items-center gap-1.5 rounded-md text-xs outline-none focus-visible:ring-2"
        >
          <ArrowLeft className="flip-rtl size-3.5" aria-hidden />
          {t('back')}
        </button>
        <CardTitle>{t(step.kind === 'MERCHANT' ? 'merchant.title' : 'courier.title')}</CardTitle>
        <CardDescription>
          {t(step.kind === 'MERCHANT' ? 'merchant.hint' : 'courier.hint')}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <RegistrationForm
          kind={step.kind}
          cities={cities}
          onDone={() => setStep({ name: 'done' })}
        />
      </CardContent>
    </Card>
  );
}

/** Étape 1 — le choix, à plat. */
function KindChoice({ onChoose }: { onChoose: (kind: RegistrationKind) => void }) {
  const t = useTranslations('auth.register');

  const options = [
    { kind: 'MERCHANT' as const, Icon: Store },
    { kind: 'COURIER' as const, Icon: Bike },
  ];

  return (
    <Card className="w-full max-w-lg">
      <CardHeader>
        <CardTitle>{t('title')}</CardTitle>
        <CardDescription>{t('chooseHint')}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3 sm:flex-row">
        {options.map(({ kind, Icon }) => (
          <button
            key={kind}
            type="button"
            onClick={() => onChoose(kind)}
            className="border-input hover:border-primary hover:bg-accent/40 focus-visible:ring-ring flex flex-1 flex-col items-start gap-2 rounded-lg border p-4 text-start transition-colors outline-none focus-visible:ring-2"
          >
            <span className="bg-primary/10 text-primary rounded-md p-2">
              <Icon className="size-5" aria-hidden />
            </span>
            <span className="font-medium">
              {t(kind === 'MERCHANT' ? 'merchant.label' : 'courier.label')}
            </span>
            <span className="text-muted-foreground text-xs">
              {t(kind === 'MERCHANT' ? 'merchant.pitch' : 'courier.pitch')}
            </span>
          </button>
        ))}
      </CardContent>
    </Card>
  );
}

/** Étape 3 — la confirmation, qui dit exactement ce qui va se passer. */
function RegistrationDone() {
  const t = useTranslations('auth.register');

  return (
    <Card className="w-full max-w-lg">
      <CardHeader className="text-center">
        <span className="bg-primary/10 text-primary mx-auto w-fit rounded-full p-3">
          <CheckCircle2 className="size-6" aria-hidden />
        </span>
        <CardTitle className="mt-3">{t('done.title')}</CardTitle>
        <CardDescription>{t('pending')}</CardDescription>
      </CardHeader>
      <CardContent className="flex justify-center">
        <Button asChild variant="outline">
          <Link href="/login">{t('done.backToLogin')}</Link>
        </Button>
      </CardContent>
    </Card>
  );
}

function RegistrationForm({
  kind,
  cities,
  onDone,
}: {
  kind: RegistrationKind;
  cities: PublicCity[];
  onDone: () => void;
}) {
  const t = useTranslations('auth.register');
  const [pending, startTransition] = useTransition();
  const [revealed, setRevealed] = useState(false);
  /*
   * Union LITTÉRALE et non `string` : les clés de traduction sont typées sur
   * `messages/fr.json`, et `t()` refuse une clé qu'on ne sait pas nommer à la
   * compilation. C'est la garantie qu'un message d'erreur sans traduction
   * casse le build plutôt que d'afficher sa propre clé à l'écran.
   */
  const [failure, setFailure] = useState<'unreachable' | 'registration-closed' | 'invalid' | null>(
    null,
  );

  const form = useForm<RegistrationInput>({
    // La sortie du schéma n'est plus son entrée (`email: null` d'un livreur,
    // point 5) : résolveur typé sur l'entrée du formulaire.
    resolver: zodResolver(registrationSchema) as unknown as Resolver<RegistrationInput>,
    /*
     * `onBlur` — C13 bloc 5. Une erreur qui n'apparaît qu'à la soumission fait
     * relire sept champs pour en trouver un ; au blur, elle se corrige là où
     * l'œil est déjà.
     */
    mode: 'onBlur',
    defaultValues:
      kind === 'MERCHANT'
        ? {
            kind: 'MERCHANT',
            shopName: '',
            firstName: '',
            lastName: '',
            phone: '',
            cityId: '',
            email: '',
            password: '',
          }
        : {
            kind: 'COURIER',
            firstName: '',
            lastName: '',
            phone: '',
            cityId: '',
            email: '',
            username: '',
            password: '',
          },
  });

  function onSubmit(values: RegistrationInput) {
    setFailure(null);

    startTransition(async () => {
      const result = await registerAction(values);

      if (result.ok) {
        onDone();
        return;
      }

      // L'adresse déjà prise se dit SOUS le champ : c'est le seul échec que la
      // personne peut corriger sans quitter des yeux ce qu'elle a tapé.
      if (result.error === 'email-taken') {
        form.setError('email', { message: 'email-taken' });
        return;
      }
      if (result.error === 'username-taken') {
        form.setError('username', { message: 'username-taken' });
        return;
      }

      setFailure(
        result.error === 'registration-closed'
          ? 'registration-closed'
          : result.error === 'unreachable'
            ? 'unreachable'
            : 'invalid',
      );
    });
  }

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-4">
        {kind === 'MERCHANT' ? (
          <FormField
            control={form.control}
            name="shopName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.shopName')}</FormLabel>
                <FormControl>
                  {/* Autofocus sur le PREMIER champ — C13 bloc 5. */}
                  <Input {...field} autoFocus autoComplete="organization" />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
        ) : null}

        <div className="grid gap-4 sm:grid-cols-2">
          <FormField
            control={form.control}
            name="firstName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.firstName')}</FormLabel>
                <FormControl>
                  <Input {...field} autoFocus={kind === 'COURIER'} autoComplete="given-name" />
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
                <FormLabel>{t('fields.phone')}</FormLabel>
                <FormControl>
                  <Input {...field} type="tel" inputMode="tel" dir="ltr" autoComplete="tel" />
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
                <FormLabel>{t('fields.city')}</FormLabel>
                <FormControl>
                  <CitySelect
                    cities={cities}
                    value={field.value}
                    onValueChange={field.onChange}
                    placeholder={t('fields.cityPlaceholder')}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
        </div>

        <FormField
          control={form.control}
          name="email"
          render={({ field }) => (
            <FormItem>
              <FormLabel>
                {kind === 'COURIER' ? t('fields.emailOptional') : t('fields.email')}
              </FormLabel>
              <FormControl>
                <Input
                  {...field}
                  value={field.value ?? ''}
                  type="email"
                  dir="ltr"
                  autoComplete="username"
                  /*
                   * Minuscules à la frappe — C13 bloc 5. Le schéma le referait
                   * à la sortie, mais l'écran ne doit pas montrer autre chose
                   * que ce qui sera enregistré.
                   */
                  onChange={(event) => field.onChange(event.target.value.toLowerCase())}
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        {/*
          LE NOM D'UTILISATEUR DU LIVREUR — recette SpeedyTrans 3, point 5.
          C'est avec lui qu'il se connectera une fois sa demande validée, s'il
          n'a pas d'adresse. L'un des deux au moins : le schéma le vérifie.
        */}
        {kind === 'COURIER' ? (
          <FormField
            control={form.control}
            name="username"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.username')}</FormLabel>
                <FormControl>
                  <Input
                    {...field}
                    value={field.value ?? ''}
                    dir="ltr"
                    autoComplete="off"
                    autoCapitalize="none"
                    spellCheck={false}
                    onChange={(event) => field.onChange(event.target.value.toLowerCase())}
                  />
                </FormControl>
                <FormDescription>{t('fields.usernameHint')}</FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />
        ) : null}

        <FormField
          control={form.control}
          name="password"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('fields.password')}</FormLabel>
              <div className="relative">
                <FormControl>
                  <Input
                    {...field}
                    type={revealed ? 'text' : 'password'}
                    dir="ltr"
                    autoComplete="new-password"
                    className="pe-10"
                  />
                </FormControl>
                {/* L'œil REMPLACE la confirmation : recopier un mot de passe à
                    l'aveugle produit surtout deux fautes de frappe identiques. */}
                <button
                  type="button"
                  onClick={() => setRevealed((value) => !value)}
                  aria-label={t(revealed ? 'fields.hidePassword' : 'fields.showPassword')}
                  className="text-muted-foreground hover:text-foreground focus-visible:ring-ring absolute inset-y-0 end-0 flex items-center rounded-md px-3 outline-none focus-visible:ring-2"
                >
                  {revealed ? (
                    <EyeOff className="size-4" aria-hidden />
                  ) : (
                    <Eye className="size-4" aria-hidden />
                  )}
                </button>
              </div>
              <p className="text-muted-foreground text-xs">
                {t('fields.passwordHint', { min: PASSWORD_MIN_LENGTH })}
              </p>
              <FormMessage />
            </FormItem>
          )}
        />

        {failure ? <p className="text-destructive text-sm">{t(`errors.${failure}`)}</p> : null}

        <Button type="submit" disabled={pending} className="gap-1.5">
          {pending ? <Loader2 className="size-4 animate-spin" /> : null}
          {t('submit')}
        </Button>
      </form>
    </Form>
  );
}
