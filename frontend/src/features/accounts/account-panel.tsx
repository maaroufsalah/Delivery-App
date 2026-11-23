'use client';

import { PASSWORD_MIN_LENGTH, type CreateUserInput, type UserRole } from '@delivery/shared';
import { Check, Copy, KeyRound, Loader2, Power, RefreshCw, UserPlus } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  AccountForm,
  type AccountFormAgency,
  type AccountFormResult,
  type AccountFormUser,
} from '@/features/accounts/account-form';
import {
  setUserActiveAction,
  setUserPasswordAction,
  updateUserAction,
} from '@/lib/actions/accounts';

/**
 * L'onglet « Compte » d'une fiche — C13 bloc 3.
 *
 * Il remplace la carte « Accès au compte », qui ne savait que poser un mot de
 * passe et affirmait, en toutes lettres, que l'adresse ne se changeait pas.
 *
 * **Sans compte lié, l'onglet le DIT** et propose de le créer, au lieu de
 * renvoyer ailleurs. C'est là que l'agent se trouve quand la personne
 * l'appelle : il a ouvert la fiche de Youssef, pas la liste des utilisateurs où
 * il faudrait le retrouver par une adresse qu'il ne connaît pas par cœur.
 *
 * **Trois gestes, trois blocs.** Le formulaire enregistre l'identité ; le mot
 * de passe a son propre bouton ; l'activation le sien. Les fondre ferait poser
 * un mot de passe en corrigeant un numéro de téléphone.
 */

export function AccountPanel({
  user,
  agencies,
  defaults,
  onCreate,
  canManage,
  fixedRole,
}: {
  /**
   * Le rôle posé d'office par la fiche (Livreur, E-commerçant) — point 5. Le
   * bloc « Rôles » disparaît du formulaire.
   */
  fixedRole?: UserRole;
  /** Compte lié à la fiche, `null` quand elle n'en a pas. */
  user: AccountFormUser | null;
  agencies: AccountFormAgency[];
  /** Prérempli depuis la fiche : e-mail, téléphone, rôle, agence. */
  defaults?: Partial<CreateUserInput>;
  /** Création : c'est la fiche qui sait à quel endpoint elle est liée. */
  onCreate?: (values: CreateUserInput) => Promise<AccountFormResult>;
  /** `accounts.write`. Sans lui, l'onglet est en lecture seule. */
  canManage: boolean;
}) {
  const t = useTranslations('accounts.account');

  if (!canManage) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('title')}</CardTitle>
          <CardDescription>{t('readOnly')}</CardDescription>
        </CardHeader>
      </Card>
    );
  }

  if (!user) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <UserPlus className="size-4" aria-hidden />
            {t('createTitle')}
          </CardTitle>
          <CardDescription>{t('createHint')}</CardDescription>
        </CardHeader>
        <CardContent>
          {onCreate ? (
            <AccountForm
              agencies={agencies}
              defaults={defaults}
              onSubmit={onCreate}
              submitLabel={t('create')}
              fixedRole={fixedRole}
            />
          ) : (
            <p className="text-muted-foreground text-sm">{t('noAccount')}</p>
          )}
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('title')}</CardTitle>
          <CardDescription>{t('hint')}</CardDescription>
        </CardHeader>
        <CardContent>
          <AccountForm
            user={user}
            agencies={agencies}
            fixedRole={fixedRole}
            onSubmit={async (values) => {
              const result = await updateUserAction(user.id, values);
              if (result.ok) toast.success(t('saved'));
              return result;
            }}
          />
        </CardContent>
      </Card>

      <PasswordCard userId={user.id} />

      <ActivationCard userId={user.id} isActive={user.isActive} />
    </div>
  );
}

/**
 * Redéfinir le mot de passe.
 *
 * En clair, avec « Générer » et « Copier » — même raisonnement qu'à la
 * création : il va être dicté au téléphone. Aucune confirmation à ressaisir,
 * puisqu'on le lit.
 */
function PasswordCard({ userId }: { userId: string }) {
  const t = useTranslations('accounts.account');
  const tCommon = useTranslations('common');
  const [pending, startTransition] = useTransition();
  const [password, setPassword] = useState('');
  const [copied, setCopied] = useState(false);

  const ALPHABET = 'ABCDEFGHJKMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789';

  const fill = () => {
    const values = new Uint32Array(12);
    crypto.getRandomValues(values);
    setPassword(Array.from(values, (value) => ALPHABET[value % ALPHABET.length]).join(''));
    setCopied(false);
  };

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(password);
      setCopied(true);
    } catch {
      setCopied(false);
    }
  };

  const save = () => {
    startTransition(async () => {
      const result = await setUserPasswordAction(userId, password);

      if (result.ok) {
        toast.success(t('passwordSaved'));
        setPassword('');
        setCopied(false);
        return;
      }

      toast.error(result.error);
    });
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <KeyRound className="size-4" aria-hidden />
          {t('passwordTitle')}
        </CardTitle>
        <CardDescription>{t('passwordHint')}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-2">
        <Label htmlFor="account-password">{t('passwordLabel')}</Label>
        <div className="flex gap-2">
          <Input
            id="account-password"
            type="text"
            dir="ltr"
            autoComplete="off"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter' && password.length >= PASSWORD_MIN_LENGTH) save();
            }}
          />
          <Button type="button" variant="outline" size="icon" onClick={fill}>
            <RefreshCw className="size-4" aria-hidden />
            <span className="sr-only">{t('generate')}</span>
          </Button>
          <Button type="button" variant="outline" size="icon" onClick={copy} disabled={!password}>
            {copied ? (
              <Check className="size-4" aria-hidden />
            ) : (
              <Copy className="size-4" aria-hidden />
            )}
            <span className="sr-only">{tCommon('copy')}</span>
          </Button>
        </div>
        <p className="text-muted-foreground text-xs">
          {t('passwordRule', { min: PASSWORD_MIN_LENGTH })}
        </p>

        <div className="flex justify-end">
          <Button
            onClick={save}
            disabled={pending || password.length < PASSWORD_MIN_LENGTH}
            className="gap-1.5"
          >
            {pending ? <Loader2 className="size-4 animate-spin" aria-hidden /> : null}
            {t('passwordSave')}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

/** Activer / désactiver. Désactiver coupe la session en cours, côté API. */
function ActivationCard({ userId, isActive }: { userId: string; isActive: boolean }) {
  const t = useTranslations('accounts.account');
  const tCommon = useTranslations('common');
  const [pending, startTransition] = useTransition();

  const toggle = () => {
    startTransition(async () => {
      const result = await setUserActiveAction(userId, !isActive);
      if (result.ok) toast.success(isActive ? t('deactivated') : t('activated'));
      else toast.error(result.error);
    });
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Power className="size-4" aria-hidden />
          {t('activationTitle')}
        </CardTitle>
        <CardDescription>{isActive ? t('activeHint') : t('inactiveHint')}</CardDescription>
      </CardHeader>
      <CardContent className="flex justify-end">
        <Button
          variant={isActive ? 'outline' : 'default'}
          onClick={toggle}
          disabled={pending}
          className="gap-1.5"
        >
          {pending ? <Loader2 className="size-4 animate-spin" aria-hidden /> : null}
          {isActive ? tCommon('deactivate') : tCommon('activate')}
        </Button>
      </CardContent>
    </Card>
  );
}

/** Rôles à proposer par défaut selon la fiche d'origine. */
export function defaultRolesFor(kind: 'client' | 'courier'): UserRole[] {
  return kind === 'client' ? ['MERCHANT'] : ['COURIER'];
}
