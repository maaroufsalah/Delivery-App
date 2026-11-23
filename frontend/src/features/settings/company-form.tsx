'use client';

import {
  ACCEPTED_LOGO_TYPES,
  type BrandPalette,
  type CompanySettings,
  type CompanySettingsPayload,
  companySettingsSchema,
  MAX_LOGO_BYTES,
  SHORT_NAME_MAX,
  SUPPORTED_COUNTRIES,
  SUPPORTED_CURRENCIES,
  SUPPORTED_TIME_ZONES,
  type ThemeSnapshot,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2, Upload } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useCallback, useRef, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { toast } from 'sonner';
import { BrandPreview } from '@/features/settings/brand-preview';
import { PaletteGrid } from '@/features/settings/palette-grid';
import { ThemeRestoreButtons } from '@/features/settings/theme-restore';
import { useCompanySettings } from '@/components/theme/company-settings-provider';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
import { routing } from '@/i18n/routing';
import { updateCompanySettingsAction, uploadCompanyLogoAction } from '@/lib/actions/settings';
import { CompanyLogo } from '@/components/layout/company-logo';

/**
 * Paramètres > Société — le white-label est ici, et nulle part ailleurs.
 *
 * Une société par ESPACE, et plusieurs espaces par installation depuis C8
 * (D-048 ; D-013 est caduque) : ce formulaire ne voit jamais que le tenant de
 * la session, l'isolation s'en charge. Les deux couleurs retenues alimentent
 * `buildThemeTokens`, qui produit tout le jeu de variables CSS ; l'aperçu les
 * applique en direct avant même l'enregistrement.
 *
 * Depuis B-10, on CHOISIT une palette plutôt que de saisir deux hexadécimaux.
 * Les sélecteurs n'ont pas disparu — ils sont rangés derrière la carte
 * « Personnalisé », pour le client qui arrive avec sa charte graphique.
 */
/**
 * Les champs du profil, dans l'ordre où on les remplit.
 *
 * Une table plutôt que huit blocs recopiés : ils ne diffèrent que par leur
 * libellé et leur `autocomplete`, et huit copies d'un même JSX finissent
 * toujours par diverger sur l'une d'elles.
 *
 * `type` reste indéfini pour le texte libre : un `dir="ltr"` forcé sur une
 * raison sociale arabe la rendrait illisible (règle RTL de CLAUDE.md). Il n'est
 * posé que sur ce qui n'est JAMAIS de la langue — téléphone, e-mail, URL.
 */
interface ProfileField {
  /** Clé du champ ET clé de traduction : `fields.<name>`. */
  name:
    | 'legalName'
    | 'taxId'
    | 'rc'
    | 'proLegalName'
    | 'proTaxId'
    | 'address'
    | 'city'
    | 'phone'
    | 'email'
    | 'website';
  autoComplete: string;
  /** Absent = texte libre, donc pas de `dir` imposé. */
  type?: 'tel' | 'email' | 'url';
}

const PROFILE_FIELDS: readonly ProfileField[] = [
  { name: 'legalName', autoComplete: 'organization' },
  { name: 'taxId', autoComplete: 'off' },
  { name: 'rc', autoComplete: 'off' },
  // D-175 : l'identité des factures PRO, facultative (vide = la standard ci-dessus).
  { name: 'proLegalName', autoComplete: 'off' },
  { name: 'proTaxId', autoComplete: 'off' },
  { name: 'address', autoComplete: 'street-address' },
  { name: 'city', autoComplete: 'address-level2' },
  { name: 'phone', autoComplete: 'tel', type: 'tel' },
  { name: 'email', autoComplete: 'email', type: 'email' },
  { name: 'website', autoComplete: 'url', type: 'url' },
];

export function CompanyForm({
  settings,
  palettes,
  standard,
  previous,
}: {
  settings: CompanySettings;
  /** Palettes lues en base (`Setting theme.presets`), jamais compilées ici. */
  palettes: BrandPalette[];
  standard: ThemeSnapshot | null;
  previous: ThemeSnapshot | null;
}) {
  const t = useTranslations('settings.company');
  const tLocale = useTranslations('locale');
  const tErrors = useTranslations('settings.errors');
  const { commitSettings, previewBrand } = useCompanySettings();
  const fileInput = useRef<HTMLInputElement>(null);
  const [saving, setSaving] = useState(false);

  const form = useForm<CompanySettingsPayload>({
    resolver: zodResolver(companySettingsSchema),
    defaultValues: {
      name: settings.name,
      shortName: settings.shortName,
      logo: settings.logo,
      logoBackground: settings.logoBackground ?? '',
      primaryColor: settings.primaryColor,
      secondaryColor: settings.secondaryColor,
      currency: settings.currency,
      country: settings.country,
      timeZone: settings.timeZone,
      locales: settings.locales as CompanySettingsPayload['locales'],
      defaultLocale: settings.defaultLocale as CompanySettingsPayload['defaultLocale'],
      /*
       * Profil (C8 bloc 3). `?? ''` et non `?? null` : un `<input>` non
       * contrôlé le redeviendrait à la première frappe, et React s'en plaint.
       * Le service refait le chemin inverse — chaîne vide = effacé.
       */
      legalName: settings.legalName ?? '',
      taxId: settings.taxId ?? '',
      rc: settings.rc ?? '',
      proLegalName: settings.proLegalName ?? '',
      proTaxId: settings.proTaxId ?? '',
      address: settings.address ?? '',
      city: settings.city ?? '',
      phone: settings.phone ?? '',
      email: settings.email ?? '',
      website: settings.website ?? '',
    },
    mode: 'onBlur',
  });

  const live = useWatch({ control: form.control });

  /** Ouvre les sélecteurs : l'admin a quitté les palettes proposées. */
  const [custom, setCustom] = useState(false);

  /**
   * Vider les surcharges par zone en même temps que la palette.
   *
   * Cochée par DÉFAUT : choisir une palette et garder une barre latérale
   * peinte à la main de l'ancienne charte produit un écran bicolore que
   * personne n'a voulu. Décocher reste possible — c'est le cas du client qui
   * ajuste sa marque sans toucher à sa charte avancée.
   */
  const [resetSections, setResetSections] = useState(true);
  const hasSections = Boolean(settings.theme && Object.keys(settings.theme).length > 0);

  const onColorChange = useCallback(
    (field: 'primaryColor' | 'secondaryColor', value: string) => {
      form.setValue(field, value, { shouldDirty: true, shouldValidate: true });
      const next = { ...form.getValues(), [field]: value };
      previewBrand(
        { primary: next.primaryColor, secondary: next.secondaryColor },
        form.getValues('theme'),
      );
    },
    [form, previewBrand],
  );

  /**
   * Choisir une palette pose les DEUX couleurs d'un coup.
   *
   * Et, si la case est cochée, vide les surcharges par zone en envoyant un
   * thème explicitement vide : `undefined` signifierait « ne touche pas », ce
   * qui est précisément le contraire de ce qui est demandé ici.
   */
  const onPaletteSelect = useCallback(
    (palette: BrandPalette) => {
      setCustom(false);
      form.setValue('primaryColor', palette.primary, { shouldDirty: true, shouldValidate: true });
      form.setValue('secondaryColor', palette.secondary, {
        shouldDirty: true,
        shouldValidate: true,
      });

      const theme = resetSections ? {} : settings.theme;
      form.setValue('theme', theme, { shouldDirty: true });

      previewBrand({ primary: palette.primary, secondary: palette.secondary }, theme);
    },
    [form, previewBrand, resetSections, settings.theme],
  );

  const onLogoSelected = useCallback(
    (file: File | undefined) => {
      if (!file) return;

      if (!ACCEPTED_LOGO_TYPES.includes(file.type)) {
        toast.error(tErrors('invalid-logo'));
        return;
      }
      if (file.size > MAX_LOGO_BYTES) {
        toast.error(tErrors('logo-too-large', { max: String(Math.round(MAX_LOGO_BYTES / 1024)) }));
        return;
      }

      /*
       * LE LOGO PART TOUT DE SUITE, par sa propre route — chantier « Audit des
       * paramètres société », bloc 3.
       *
       * Il ne voyage plus en data URL dans le formulaire : il est rangé comme
       * un FICHIER sur le volume persistant, et la configuration ne garde que
       * son URL. C'est ce qui évite de réémettre 683 ko à chaque changement de
       * couleur — le corps qui heurtait la limite du body-parser.
       *
       * L'aperçu est immédiat parce que l'API rend la configuration à jour :
       * on repose l'URL dans le formulaire, et la marque est revalidée.
       */
      const reader = new FileReader();
      reader.onload = async () => {
        // `data:<type>;base64,<charge>` — on ne garde que la charge utile.
        const content = String(reader.result).split(',')[1] ?? '';

        const result = await uploadCompanyLogoAction({
          name: file.name,
          mimeType: file.type,
          content,
        });

        if (!result.ok) {
          toast.error(
            result.error && result.error !== 'invalid' ? result.error : tErrors('invalid-logo'),
          );
          return;
        }

        form.setValue('logo', result.data.logo, { shouldDirty: false, shouldValidate: true });
        commitSettings(result.data);
        toast.success(t('saved'));
      };
      reader.readAsDataURL(file);
    },
    [commitSettings, form, t, tErrors],
  );

  async function onSubmit(values: CompanySettingsPayload) {
    setSaving(true);
    try {
      const result = await updateCompanySettingsAction(values);

      if (result.ok) {
        commitSettings(result.data);
        form.reset(values);
        toast.success(t('saved'));
      } else {
        /*
         * LE VRAI MESSAGE, pas « échec ». L'API dit pourquoi — « Le contenu
         * envoyé est trop volumineux », « Saisie invalide » — et c'est cela
         * qu'il faut lire. Le libellé générique ne reste que pour les refus
         * sans message, et pour `invalid`, qui est un mot de code.
         */
        toast.error(result.error && result.error !== 'invalid' ? result.error : t('saveFailed'));
      }
    } finally {
      setSaving(false);
    }
  }

  function onReset() {
    form.reset();
    previewBrand(null);
  }

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-6">
        <Card>
          <CardHeader>
            <CardTitle>{t('identity.title')}</CardTitle>
            <CardDescription>{t('identity.description')}</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-5 md:grid-cols-2">
            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.name')}</FormLabel>
                  <FormControl>
                    <Input {...field} autoComplete="organization" />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            {/* Lot D, D2 (D-189) : « Reçu par … », « Remis à … », « Créé par … ». */}
            <FormField
              control={form.control}
              name="shortName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.shortName')}</FormLabel>
                  <FormControl>
                    <Input {...field} maxLength={SHORT_NAME_MAX} />
                  </FormControl>
                  <FormDescription>{t('fields.shortNameHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="logo"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.logo')}</FormLabel>
                  <FormControl>
                    <div className="flex items-center gap-3">
                      <span className="bg-muted flex h-12 min-w-12 shrink-0 items-center justify-center overflow-hidden rounded-md border px-1">
                        <CompanyLogo
                          src={field.value}
                          background={form.watch('logoBackground') || null}
                          size="md"
                        />
                      </span>
                      <input
                        ref={fileInput}
                        type="file"
                        accept={ACCEPTED_LOGO_TYPES.join(',')}
                        className="sr-only"
                        onChange={(event) => onLogoSelected(event.target.files?.[0])}
                      />
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        className="gap-1.5"
                        onClick={() => fileInput.current?.click()}
                      >
                        <Upload className="size-4" />
                        {t('fields.uploadLogo')}
                      </Button>
                    </div>
                  </FormControl>
                  <FormDescription>{t('fields.logoHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            {/*
              LE FOND DU LOGO — dernières corrections, point 5. Un logo clair
              sur transparence disparaît sur un fond clair : on ne le recolore
              pas, on lui donne un fond. Vide = le logo s'affiche tel quel.
            */}
            <FormField
              control={form.control}
              name="logoBackground"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.logoBackground')}</FormLabel>
                  <FormControl>
                    <div className="flex items-center gap-2">
                      <div className="min-w-0 flex-1">
                        <ColorField
                          value={field.value || ''}
                          onChange={(value) => field.onChange(value)}
                        />
                      </div>
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        disabled={!field.value}
                        onClick={() => field.onChange('')}
                      >
                        {t('fields.logoBackgroundNone')}
                      </Button>
                    </div>
                  </FormControl>
                  <FormDescription>{t('fields.logoBackgroundHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        {/*
          Le profil légal — C8 bloc 3.

          Ce n'est pas de la décoration administrative : ces champs sont ce qui
          part sur les FACTURES, les bordereaux et le pied des PDF. Une société
          marocaine doit y faire figurer son ICE, et l'adresse imprimée est
          celle-ci — plus aucune chaîne en dur nulle part.

          Tout est facultatif : un espace en essai n'a pas encore rempli sa
          fiche, et l'application doit fonctionner quand même. Chaque document
          omet la ligne qu'il n'a pas plutôt que d'imprimer un libellé vide.
        */}
        <Card>
          <CardHeader>
            <CardTitle>{t('profile.title')}</CardTitle>
            <CardDescription>{t('profile.description')}</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-5 md:grid-cols-2">
            {PROFILE_FIELDS.map(({ name, autoComplete, type }) => (
              <FormField
                key={name}
                control={form.control}
                name={name}
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t(`fields.${name}`)}</FormLabel>
                    <FormControl>
                      <Input
                        {...field}
                        value={field.value ?? ''}
                        type={type ?? 'text'}
                        autoComplete={autoComplete}
                        dir={type === undefined ? undefined : 'ltr'}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div className="min-w-0">
              <CardTitle>{t('palettes.title')}</CardTitle>
              <CardDescription>{t('palettes.description')}</CardDescription>
            </div>
            <div className="flex flex-wrap items-center gap-1">
              <ThemeRestoreButtons
                standard={standard}
                previous={previous}
                onRestored={(next) => {
                  commitSettings(next);
                  form.reset({
                    ...form.getValues(),
                    primaryColor: next.primaryColor,
                    secondaryColor: next.secondaryColor,
                    theme: next.theme,
                  });
                  setCustom(false);
                }}
              />
            </div>
          </CardHeader>

          <CardContent className="flex flex-col gap-4">
            <PaletteGrid
              palettes={palettes}
              primary={live.primaryColor ?? settings.primaryColor}
              secondary={live.secondaryColor ?? settings.secondaryColor}
              custom={custom}
              onSelect={onPaletteSelect}
              onCustom={() => setCustom(true)}
            />

            {hasSections ? (
              <label className="flex items-start gap-2 text-sm">
                <Checkbox
                  checked={resetSections}
                  onCheckedChange={(next) => setResetSections(next === true)}
                  className="mt-0.5"
                />
                <span>
                  {t('palettes.resetSections')}
                  <span className="text-muted-foreground block text-xs">
                    {t('palettes.resetSectionsHint')}
                  </span>
                </span>
              </label>
            ) : null}

            {/* Les sélecteurs ne sont pas supprimés : ils sont rangés. Le
                client qui arrive avec sa charte les retrouve intacts. */}
            {custom ? (
              <div className="grid gap-5 border-t pt-4 md:grid-cols-2">
                <FormField
                  control={form.control}
                  name="primaryColor"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('fields.primaryColor')}</FormLabel>
                      <FormControl>
                        <ColorField
                          value={field.value}
                          onChange={(value) => onColorChange('primaryColor', value)}
                        />
                      </FormControl>
                      <FormDescription>{t('fields.primaryHint')}</FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <FormField
                  control={form.control}
                  name="secondaryColor"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('fields.secondaryColor')}</FormLabel>
                      <FormControl>
                        <ColorField
                          value={field.value}
                          onChange={(value) => onColorChange('secondaryColor', value)}
                        />
                      </FormControl>
                      <FormDescription>{t('fields.secondaryHint')}</FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
            ) : null}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t('locale.title')}</CardTitle>
            <CardDescription>{t('locale.description')}</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-5 md:grid-cols-3">
            <FormField
              control={form.control}
              name="currency"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.currency')}</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger className="w-full">
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {SUPPORTED_CURRENCIES.map((code) => (
                        <SelectItem key={code} value={code}>
                          {code}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="country"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.country')}</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger className="w-full">
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {SUPPORTED_COUNTRIES.map((code) => (
                        <SelectItem key={code} value={code}>
                          {t(`countries.${code}`)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="timeZone"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.timeZone')}</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger className="w-full">
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {SUPPORTED_TIME_ZONES.map((zone) => (
                        <SelectItem key={zone} value={zone}>
                          {zone}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="locales"
              render={({ field }) => (
                <FormItem className="md:col-span-2">
                  <FormLabel>{t('fields.locales')}</FormLabel>
                  <div className="flex flex-wrap gap-4 pt-1">
                    {routing.locales.map((code) => {
                      const checked = field.value.includes(code);
                      return (
                        <label
                          key={code}
                          className="flex cursor-pointer items-center gap-2 text-sm"
                        >
                          <Checkbox
                            checked={checked}
                            onCheckedChange={(next) => {
                              field.onChange(
                                next
                                  ? [...field.value, code]
                                  : field.value.filter((item) => item !== code),
                              );
                            }}
                          />
                          {tLocale(code)}
                        </label>
                      );
                    })}
                  </div>
                  <FormDescription>{t('fields.localesHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="defaultLocale"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.defaultLocale')}</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger className="w-full">
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {routing.locales.map((code) => (
                        <SelectItem key={code} value={code}>
                          {tLocale(code)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        <BrandPreview
          name={live.name ?? settings.name}
          logo={live.logo ?? settings.logo}
          logoBackground={form.watch('logoBackground') || null}
          primaryColor={live.primaryColor ?? settings.primaryColor}
          secondaryColor={live.secondaryColor ?? settings.secondaryColor}
        />

        <div className="bg-background/95 sticky bottom-20 z-10 flex flex-wrap items-center gap-2 rounded-lg border p-3 backdrop-blur md:bottom-4">
          <Button type="submit" disabled={saving || !form.formState.isDirty} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : null}
            {t('save')}
          </Button>
          <Button
            type="button"
            variant="ghost"
            onClick={onReset}
            disabled={saving || !form.formState.isDirty}
          >
            {t('discard')}
          </Button>
          <p className="text-muted-foreground ms-auto text-xs">{t('saveHint')}</p>
        </div>
      </form>
    </Form>
  );
}

/** Sélecteur natif + saisie hexadécimale : les deux écrivent la même valeur. */
function ColorField({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  return (
    <div className="flex items-center gap-2">
      <input
        type="color"
        // Le fond du logo peut être vide : le sélecteur natif exige une couleur.
        value={value || '#FFFFFF'}
        onChange={(event) => onChange(event.target.value.toUpperCase())}
        className="border-input size-9 shrink-0 cursor-pointer rounded-md border bg-transparent p-1"
        aria-hidden
        tabIndex={-1}
      />
      <Input
        value={value}
        onChange={(event) => onChange(event.target.value.toUpperCase())}
        className="font-mono uppercase"
        spellCheck={false}
      />
    </div>
  );
}
