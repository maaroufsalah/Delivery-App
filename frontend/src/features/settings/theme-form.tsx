'use client';

import {
  buildThemeTokens,
  type CompanySettings,
  contrastRatio,
  isThemeEmpty,
  MIN_CONTRAST,
  normalizeHex,
  pruneTheme,
  THEME_PRESETS,
  type ThemeOverrides,
  type ThemeSections,
  type ThemeSnapshot,
} from '@delivery/shared';
import { Loader2, Moon, RotateCcw, Sun } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useMemo, useState } from 'react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ThemePreview } from '@/features/settings/theme-preview';
import { ThemeRestoreButtons } from '@/features/settings/theme-restore';
import { useCompanySettings } from '@/components/theme/company-settings-provider';
import { updateCompanyThemeAction } from '@/lib/actions/settings';
import { cn } from '@/lib/utils';

/**
 * Paramètres > Société > Thème — le white-label au-delà des deux couleurs.
 *
 * Ce que l'écran doit rendre évident, et que le reste découle :
 *  - on règle des ZONES (barre latérale, en-tête, tableaux, boutons, badges),
 *    pas trente-six tokens shadcn que personne ne sait nommer ;
 *  - le CLAIR et le SOMBRE sont visibles ensemble, parce qu'un admin règle en
 *    clair et découvre son sombre illisible le lendemain ;
 *  - un champ vide n'est pas « blanc », c'est « dérivé de la marque ». Le
 *    bouton de remise à zéro d'une section vide ses champs, et la couleur
 *    revient d'elle-même ;
 *  - un fond posé sans son texte fait CALCULER le texte. L'admin n'a pas à
 *    connaître WCAG ; il a seulement le droit de ne pas être trahi.
 */

type Mode = 'light' | 'dark';

interface Field {
  section: keyof ThemeSections;
  key: string;
  /** Le champ de texte que ce fond doit rester capable de porter. */
  pairedWith?: string;
}

const FIELDS: Record<keyof ThemeSections, Field[]> = {
  sidebar: [
    { section: 'sidebar', key: 'background' },
    { section: 'sidebar', key: 'foreground', pairedWith: 'background' },
    { section: 'sidebar', key: 'accent' },
    { section: 'sidebar', key: 'border' },
  ],
  header: [
    { section: 'header', key: 'background' },
    { section: 'header', key: 'foreground', pairedWith: 'background' },
    { section: 'header', key: 'border' },
  ],
  table: [
    { section: 'table', key: 'headerBackground' },
    { section: 'table', key: 'headerForeground', pairedWith: 'headerBackground' },
    { section: 'table', key: 'rowHover' },
    { section: 'table', key: 'border' },
  ],
  button: [
    { section: 'button', key: 'background' },
    { section: 'button', key: 'foreground', pairedWith: 'background' },
  ],
  badge: [
    { section: 'badge', key: 'background' },
    { section: 'badge', key: 'foreground', pairedWith: 'background' },
  ],
};

const SECTIONS = Object.keys(FIELDS) as Array<keyof ThemeSections>;

type Draft = Record<Mode, Record<string, Record<string, string>>>;

function toDraft(theme: ThemeOverrides | undefined): Draft {
  const read = (mode: Mode) => {
    const sections = theme?.[mode];
    const result: Record<string, Record<string, string>> = {};

    for (const section of SECTIONS) {
      const values = (sections?.[section] ?? {}) as Record<string, string>;
      result[section] = { ...values };
    }

    return result;
  };

  return { light: read('light'), dark: read('dark') };
}

function toTheme(draft: Draft): ThemeOverrides {
  return pruneTheme({
    light: draft.light as ThemeSections,
    dark: draft.dark as ThemeSections,
  });
}

export function ThemeForm({
  settings,
  standard,
  previous,
}: {
  settings: CompanySettings;
  standard: ThemeSnapshot | null;
  previous: ThemeSnapshot | null;
}) {
  const t = useTranslations('settings.theme');
  const tError = useTranslations('validation');
  const { commitSettings } = useCompanySettings();

  const [draft, setDraft] = useState<Draft>(() => toDraft(settings.theme));
  const [mode, setMode] = useState<Mode>('light');
  const [saving, setSaving] = useState(false);

  const theme = useMemo(() => toTheme(draft), [draft]);

  const tokens = useMemo(
    () =>
      buildThemeTokens(
        { primary: settings.primaryColor, secondary: settings.secondaryColor },
        theme,
      ),
    [settings.primaryColor, settings.secondaryColor, theme],
  );

  /** Les couples fond/texte illisibles, repérés au fil de la saisie. */
  const unreadable = useMemo(() => {
    const found: string[] = [];

    for (const section of SECTIONS) {
      for (const field of FIELDS[section]) {
        if (!field.pairedWith) continue;

        const values = draft[mode][section] ?? {};
        const background = values[field.pairedWith];
        const foreground = values[field.key];

        if (!background || !foreground) continue;
        if (contrastRatio(background, foreground) < MIN_CONTRAST) {
          found.push(`${section}.${field.key}`);
        }
      }
    }

    return found;
  }, [draft, mode]);

  const setValue = (section: string, key: string, value: string) => {
    setDraft((current) => ({
      ...current,
      [mode]: {
        ...current[mode],
        [section]: { ...current[mode][section], [key]: value },
      },
    }));
  };

  const resetSection = (section: string) => {
    setDraft((current) => ({
      ...current,
      [mode]: { ...current[mode], [section]: {} },
    }));
  };

  const resetAll = () => setDraft(toDraft(undefined));

  const applyPreset = (preset: ThemeOverrides) => setDraft(toDraft(preset));

  const save = async () => {
    if (unreadable.length > 0) {
      toast.error(t('errors.contrast'));
      return;
    }

    setSaving(true);
    const result = await updateCompanyThemeAction(theme);
    setSaving(false);

    if (result.ok) {
      toast.success(t('saved'));
      return;
    }

    // Le refus de rôle se reconnaît au CODE de l'API, plus à un mot sentinelle
    // que l'action fabriquait ; tout le reste affiche le message de l'API.
    toast.error(
      result.code === 'FORBIDDEN'
        ? t('errors.forbidden')
        : result.error === 'invalid'
          ? tError('invalid')
          : (result.error ?? t('errors.unexpected')),
    );
  };

  return (
    <div className="flex flex-col gap-6">
      <Card>
        <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div className="min-w-0">
            <CardTitle>{t('title')}</CardTitle>
            <CardDescription>{t('description')}</CardDescription>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            {isThemeEmpty(theme) ? (
              <Badge variant="secondary" className="rounded-full font-normal">
                {t('derived')}
              </Badge>
            ) : (
              <Button variant="ghost" size="sm" className="gap-1.5" onClick={resetAll}>
                <RotateCcw className="size-4" aria-hidden />
                {t('resetAll')}
              </Button>
            )}

            {/* Le retour GLOBAL — couleurs de marque comprises — en plus de la
                remise à zéro des seules zones ci-contre (B-10). */}
            <ThemeRestoreButtons
              standard={standard}
              previous={previous}
              onRestored={(next) => {
                commitSettings(next);
                setDraft(toDraft(next.theme));
              }}
            />
            <Button onClick={save} disabled={saving} size="sm" className="gap-1.5">
              {saving ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('save')}
            </Button>
          </div>
        </CardHeader>

        <CardContent className="flex flex-col gap-3">
          <p className="text-muted-foreground text-xs">{t('presetsHint')}</p>
          <div className="flex flex-wrap gap-2">
            {THEME_PRESETS.map((preset) => (
              <Button
                key={preset.key}
                variant="outline"
                size="sm"
                onClick={() => applyPreset(preset.theme)}
              >
                {t(`presets.${preset.key}` as 'presets.slate')}
              </Button>
            ))}
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-6 lg:grid-cols-2">
        <ThemePreview tokens={tokens.light} label={t('modes.light')} />
        <ThemePreview tokens={tokens.dark} label={t('modes.dark')} />
      </div>

      <Card>
        <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="min-w-0">
            <CardTitle className="text-base">{t('edit.title')}</CardTitle>
            <CardDescription>
              {mode === 'light' ? t('edit.lightHint') : t('edit.darkHint')}
            </CardDescription>
          </div>

          {/* Le mode édité, pas le mode d'affichage : les deux aperçus restent
              visibles en permanence au-dessus. */}
          <div className="bg-muted flex w-fit rounded-lg p-1" role="tablist">
            {(['light', 'dark'] as const).map((value) => {
              const Icon = value === 'light' ? Sun : Moon;
              return (
                <button
                  key={value}
                  type="button"
                  role="tab"
                  aria-selected={mode === value}
                  onClick={() => setMode(value)}
                  className={cn(
                    'flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm transition-colors',
                    mode === value
                      ? 'bg-background shadow-sm'
                      : 'text-muted-foreground hover:text-foreground',
                  )}
                >
                  <Icon className="size-4" aria-hidden />
                  {t(`modes.${value}`)}
                </button>
              );
            })}
          </div>
        </CardHeader>

        <CardContent className="flex flex-col gap-6">
          {SECTIONS.map((section) => {
            const values = draft[mode][section] ?? {};
            const touched = Object.values(values).some(Boolean);

            return (
              <section key={section} className="flex flex-col gap-3">
                <div className="flex items-center justify-between gap-2">
                  <h3 className="text-sm font-medium">
                    {t(`sections.${section}` as 'sections.sidebar')}
                  </h3>
                  <Button
                    variant="ghost"
                    size="sm"
                    disabled={!touched}
                    onClick={() => resetSection(section)}
                    className="gap-1.5"
                  >
                    <RotateCcw className="size-3.5" aria-hidden />
                    {t('reset')}
                  </Button>
                </div>

                <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                  {FIELDS[section].map((field) => (
                    <ColorField
                      key={field.key}
                      id={`${mode}-${section}-${field.key}`}
                      label={t(`fields.${field.key}` as 'fields.background')}
                      value={values[field.key] ?? ''}
                      invalid={unreadable.includes(`${section}.${field.key}`)}
                      onChange={(value) => setValue(section, field.key, value)}
                    />
                  ))}
                </div>

                {mode === 'dark' && !touched ? (
                  <p className="text-muted-foreground text-xs">{t('edit.derivedFromLight')}</p>
                ) : null}
              </section>
            );
          })}

          {unreadable.length > 0 ? (
            <p className="border-destructive/40 bg-destructive/5 text-destructive rounded-md border px-3 py-2 text-xs">
              {t('errors.contrast')}
            </p>
          ) : null}
        </CardContent>
      </Card>
    </div>
  );
}

/**
 * Un sélecteur de couleur et son champ texte, liés.
 *
 * Le champ texte existe parce qu'une charte graphique arrive en hexadécimal,
 * pas en pipette : un admin colle `#0F172A`, il ne le cherche pas à la souris.
 * Vide = « dérivé de la marque », et c'est l'état par défaut.
 */
function ColorField({
  id,
  label,
  value,
  invalid,
  onChange,
}: {
  id: string;
  label: string;
  value: string;
  invalid: boolean;
  onChange: (value: string) => void;
}) {
  const t = useTranslations('settings.theme');

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id} className="text-xs">
        {label}
      </Label>
      <div className="flex items-center gap-2">
        <input
          type="color"
          aria-label={label}
          value={value || '#FFFFFF'}
          onChange={(event) => onChange(normalizeHex(event.target.value, '#FFFFFF'))}
          className="border-input size-9 shrink-0 cursor-pointer rounded-md border bg-transparent p-1"
        />
        <Input
          id={id}
          value={value}
          placeholder={t('inherit')}
          onChange={(event) => onChange(event.target.value.trim())}
          aria-invalid={invalid}
          className={cn('font-mono text-xs', invalid && 'border-destructive')}
        />
      </div>
    </div>
  );
}
