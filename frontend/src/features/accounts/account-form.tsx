'use client';

import {
  type CreateUserInput,
  createUserSchema,
  editUserFormSchema,
  PASSWORD_MIN_LENGTH,
  TENANT_ROLES,
  type UserDetail,
  type UserListItem,
  type UserRole,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Check, Copy, Loader2, RefreshCw } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { type Resolver, useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { ALL_VALUE } from '@/components/data-table/data-table-toolbar';
import { SearchableSelect } from '@/components/searchable-select';
import { Button } from '@/components/ui/button';
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

/**
 * LE formulaire de compte — C13 bloc 3.
 *
 * Un seul, pour les quatre endroits où l'on touche à une identité : l'onglet
 * « Compte » d'une fiche e-commerçant, celui d'une fiche livreur, celui d'un
 * membre de l'équipe, et la page Utilisateurs qui reste la vue globale. Le
 * chantier précédent en avait trois — un formulaire d'invitation, une boîte de
 * dialogue à un seul champ, une carte de mot de passe — et ils ne racontaient
 * déjà plus la même histoire : l'un annonçait que l'adresse ne se change pas,
 * l'autre qu'aucun mot de passe ne se saisit ici, tous deux faux depuis D-086.
 *
 * Ce qu'il fait, et ce qu'il ne fait PAS :
 *
 * - il crée (mot de passe compris) et il modifie (adresse comprise) ;
 * - il ne redéfinit PAS un mot de passe existant, et n'active ni ne désactive :
 *   ces gestes-là ne se confondent pas avec « enregistrer une fiche ». Poser un
 *   mot de passe en corrigeant un numéro de téléphone déconnecterait la
 *   personne sans prévenir.
 */

/**
 * Ce que le formulaire a besoin de connaître d'un compte existant.
 *
 * Une ligne de LISTE suffit, à `nationalId` près — que seule la fiche porte.
 * Exiger un `UserDetail` obligerait la page Utilisateurs à charger chaque
 * compte pour ouvrir son formulaire ; l'accepter optionnel laisse les deux
 * appelants nourrir ce qu'ils ont.
 */
export type AccountFormUser = UserListItem & Partial<Pick<UserDetail, 'nationalId'>>;

/**
 * Ce que le formulaire a besoin de connaître d'une AGENCE : son identifiant et
 * son nom. Exiger `AgencyListItem` obligerait chaque appelant à charger la
 * forme lourde — et une fiche livreur, qui a déjà la liste légère du
 * référentiel, aurait dû la redemander pour rien.
 */
export type AccountFormAgency = { id: string; name: string };

export interface AccountFormResult {
  ok: boolean;
  error?: string;
  field?: string;
}

/**
 * Mot de passe proposé par le bouton « Générer ».
 *
 * Il s'affiche EN CLAIR dans le champ, et c'est tout l'intérêt : il va être
 * dicté au téléphone. Alphabet volontairement sans `I`, `l`, `O` ni `0` — un
 * mot de passe qui se dicte ne doit pas se jouer sur la forme d'un caractère.
 */
const ALPHABET = 'ABCDEFGHJKMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789';

function generatePassword(length = 12): string {
  const values = new Uint32Array(length);
  crypto.getRandomValues(values);

  return Array.from(values, (value) => ALPHABET[value % ALPHABET.length]).join('');
}

export function AccountForm({
  user,
  agencies,
  defaults,
  onSubmit,
  onCancel,
  submitLabel,
  fixedRole,
}: {
  /** Compte existant : le formulaire passe en modification, sans mot de passe. */
  user?: AccountFormUser | null;
  agencies: AccountFormAgency[];
  /**
   * Valeurs préremplies depuis la FICHE, à la création — e-mail, téléphone,
   * rôle et agence. L'administrateur peut les corriger : ce qui est prérempli
   * est une proposition, pas une contrainte.
   */
  defaults?: Partial<CreateUserInput>;
  onSubmit: (values: CreateUserInput) => Promise<AccountFormResult>;
  onCancel?: () => void;
  submitLabel?: string;
  /**
   * LE RÔLE DE LA FICHE — recette SpeedyTrans 3, point 5.
   *
   * Sur les fiches Livreur et Client, le rôle est posé d'office (Livreur,
   * E-commerçant) et le bloc « Rôles » disparaît : il invitait à cocher
   * n'importe quoi sur un compte qui n'a qu'un sens. L'API refuse d'ailleurs
   * tout autre rôle sur ces deux fiches. Mon équipe, Personnel et Utilisateurs
   * gardent le bloc.
   */
  fixedRole?: UserRole;
}) {
  const t = useTranslations('accounts.users');
  const tRole = useTranslations('accounts.roles.names');
  const tCommon = useTranslations('common');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);
  const [copied, setCopied] = useState(false);

  /*
   * DEUX schémas pour UN formulaire : le mot de passe est obligatoire à la
   * création et absent de la modification. Valider une modification avec le
   * schéma de création exigerait un mot de passe qu'on ne demande pas — et le
   * contourner par une valeur bidon ferait passer un secret factice sur le
   * réseau à chaque enregistrement.
   */
  const schema = user ? editUserFormSchema : createUserSchema;

  const form = useForm<CreateUserInput>({
    resolver: zodResolver(schema) as unknown as Resolver<CreateUserInput>,
    // Au blur — C13 bloc 5 : une erreur trouvée à la soumission fait relire
    // huit champs pour en corriger un.
    mode: 'onBlur',
    defaultValues: user
      ? {
          email: user.email ?? '',
          username: user.username ?? '',
          firstName: user.firstName,
          lastName: user.lastName,
          phone: user.phone ?? '',
          jobTitle: user.jobTitle ?? '',
          nationalId: user.nationalId ?? '',
          roles: fixedRole ? [fixedRole] : user.roles,
          agencyId: user.agencyId ?? '',
          agencyIds: user.agencyIds,
          password: '',
        }
      : {
          email: '',
          username: '',
          firstName: '',
          lastName: '',
          phone: '',
          jobTitle: '',
          nationalId: '',
          roles: [],
          agencyId: '',
          agencyIds: [],
          password: '',
          ...defaults,
          ...(fixedRole ? { roles: [fixedRole] } : {}),
        },
  });

  const roles = (form.watch('roles') ?? []) as UserRole[];
  /*
   * Le nom d'utilisateur se propose pour un compte PUREMENT livreur — le seul
   * qui puisse se passer d'e-mail (point 5) — et reste visible sur un compte
   * qui en porte déjà un, pour qu'on puisse le lire et le corriger.
   */
  const courierOnly = roles.length === 1 && roles[0] === 'COURIER';
  const showUsername = courierOnly || Boolean(user?.username);
  const agencyIds = (form.watch('agencyIds') ?? []) as string[];
  const password = form.watch('password') ?? '';

  const toggleRole = (role: UserRole, checked: boolean) => {
    const next = checked ? [...new Set([...roles, role])] : roles.filter((entry) => entry !== role);
    form.setValue('roles', next, { shouldDirty: true, shouldValidate: true });
  };

  const toggleAgency = (agencyId: string, checked: boolean) => {
    const next = checked
      ? [...new Set([...agencyIds, agencyId])]
      : agencyIds.filter((entry) => entry !== agencyId);
    form.setValue('agencyIds', next, { shouldDirty: true });
  };

  const fill = () => {
    form.setValue('password', generatePassword(), { shouldDirty: true, shouldValidate: true });
    setCopied(false);
  };

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(password);
      setCopied(true);
    } catch {
      // Presse-papiers refusé : le mot de passe reste lisible à l'écran, qui
      // est de toute façon le vrai moyen de le transmettre.
      setCopied(false);
    }
  };

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await onSubmit(values);
    setSaving(false);

    if (result.ok) return;

    if (result.field === 'email') form.setError('email', { message: result.error });
    else if (result.field === 'username') form.setError('username', { message: result.error });
    else if (result.field === 'roles') form.setError('roles', { message: result.error });
    else toast.error(result.error === 'invalid' ? tError('invalid') : result.error);
  });

  return (
    <Form {...form}>
      {/* `onKeyDown` plutôt qu'un bouton implicite : le formulaire vit parfois
          dans un onglet, où un `type="submit"` isolé n'est rattaché à rien.
          Entrée soumet — C13 bloc 5. */}
      <form
        className="flex flex-col gap-4"
        onSubmit={submit}
        onKeyDown={(event) => {
          if (
            event.key === 'Enter' &&
            !event.shiftKey &&
            event.target instanceof HTMLInputElement
          ) {
            event.preventDefault();
            void submit();
          }
        }}
      >
        <FormField
          control={form.control}
          name="email"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('fields.email')}</FormLabel>
              <FormControl>
                <Input
                  {...field}
                  value={field.value ?? ''}
                  type="email"
                  dir="ltr"
                  autoFocus={!user}
                  autoComplete="off"
                  onChange={(event) => field.onChange(event.target.value.toLowerCase())}
                />
              </FormControl>
              <FormDescription>
                {courierOnly ? t('fields.emailOptionalHint') : t('fields.emailHint')}
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        {showUsername ? (
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

        {user ? null : (
          <FormField
            control={form.control}
            name="password"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.password')}</FormLabel>
                <div className="flex gap-2">
                  <FormControl>
                    {/*
                      EN CLAIR, volontairement. Il va être dicté au téléphone,
                      et un champ masqué obligerait à le retaper ailleurs pour
                      le relire — c'est-à-dire à le recopier de travers.
                    */}
                    <Input {...field} type="text" dir="ltr" autoComplete="off" />
                  </FormControl>
                  <Button type="button" variant="outline" size="icon" onClick={fill}>
                    <RefreshCw className="size-4" aria-hidden />
                    <span className="sr-only">{t('fields.generate')}</span>
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    size="icon"
                    onClick={copy}
                    disabled={!password}
                  >
                    {copied ? (
                      <Check className="size-4" aria-hidden />
                    ) : (
                      <Copy className="size-4" aria-hidden />
                    )}
                    <span className="sr-only">{tCommon('copy')}</span>
                  </Button>
                </div>
                <FormDescription>
                  {t('fields.passwordHint', { min: PASSWORD_MIN_LENGTH })}
                </FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />
        )}

        <div className="grid gap-3 sm:grid-cols-2">
          <FormField
            control={form.control}
            name="firstName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.firstName')}</FormLabel>
                <FormControl>
                  <Input {...field} autoFocus={Boolean(user)} />
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
                  <Input {...field} />
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
                  <Input {...field} type="tel" inputMode="tel" dir="ltr" />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="jobTitle"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.jobTitle')}</FormLabel>
                <FormControl>
                  <Input {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
        </div>

        {fixedRole ? null : (
          <FormField
            control={form.control}
            name="roles"
            render={() => (
              <FormItem>
                <FormLabel>{t('fields.roles')}</FormLabel>
                <FormDescription>{t('fields.rolesHint')}</FormDescription>
                <div className="flex flex-col gap-1 rounded-lg border p-2">
                  {TENANT_ROLES.map((role) => (
                    <label
                      key={role}
                      className="hover:bg-accent/60 flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm"
                    >
                      <Checkbox
                        checked={roles.includes(role)}
                        onCheckedChange={(checked) => toggleRole(role, checked === true)}
                      />
                      <span>{tRole(role)}</span>
                    </label>
                  ))}
                </div>
                <FormMessage />
              </FormItem>
            )}
          />
        )}

        <FormField
          control={form.control}
          name="agencyId"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('fields.agency')}</FormLabel>
              <FormControl>
                <SearchableSelect
                  options={agencies.map((agency) => ({ value: agency.id, label: agency.name }))}
                  value={field.value || ALL_VALUE}
                  onValueChange={(value) => field.onChange(value === ALL_VALUE ? '' : value)}
                  placeholder={t('fields.agency')}
                  emptyOption={{ value: ALL_VALUE, label: t('fields.noAgency') }}
                  className="w-full"
                />
              </FormControl>
              <FormDescription>{t('fields.agencyHint')}</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="agencyIds"
          render={() => (
            <FormItem>
              <FormLabel>{t('fields.agencies')}</FormLabel>
              <FormDescription>{t('fields.agenciesHint')}</FormDescription>
              <div className="flex flex-col gap-1 rounded-lg border p-2">
                {agencies.map((agency) => (
                  <label
                    key={agency.id}
                    className="hover:bg-accent/60 flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm"
                  >
                    <Checkbox
                      checked={agencyIds.includes(agency.id)}
                      onCheckedChange={(checked) => toggleAgency(agency.id, checked === true)}
                    />
                    <span className="truncate">{agency.name}</span>
                  </label>
                ))}
              </div>
              <FormMessage />
            </FormItem>
          )}
        />

        <div className="flex justify-end gap-2">
          {onCancel ? (
            <Button type="button" variant="outline" onClick={onCancel} disabled={saving}>
              {tCommon('cancel')}
            </Button>
          ) : null}
          <Button type="submit" disabled={saving} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" aria-hidden /> : null}
            {submitLabel ?? tCommon('save')}
          </Button>
        </div>
      </form>
    </Form>
  );
}
