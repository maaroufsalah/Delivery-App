'use client';

import { Moon, Sun } from 'lucide-react';
import { useLocale, useTranslations } from 'next-intl';
import { useParams } from 'next/navigation';
import { useTheme } from 'next-themes';
import { useTransition } from 'react';
import { CellBadge } from '@/components/data-table/cells';
import { useCompanySettings } from '@/components/theme/company-settings-provider';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { usePathname, useRouter } from '@/i18n/navigation';
import { routing } from '@/i18n/routing';
import { useMounted } from '@/hooks/use-mounted';
import { cn } from '@/lib/utils';

/**
 * Deux modes, plus trois — C9 bloc 2.
 *
 * « Système » a été retiré : il faisait dépendre l'apparence d'un réglage d'OS
 * que l'utilisateur n'associe pas à cette application, et personne ne le
 * rapprochait de l'écran devenu sombre tout seul.
 */
const MODES = [
  { value: 'light', icon: Sun },
  { value: 'dark', icon: Moon },
] as const;

/**
 * Paramètres > Apparence — préférences de l'UTILISATEUR uniquement.
 *
 * La marque n'est plus ici : elle est une configuration d'installation
 * (Paramètres > Société, docs/DECISIONS.md D-013). Ne restent que la langue et
 * le mode d'affichage, plus un aperçu.
 *
 * Les langues proposées sont celles activées dans la configuration société ;
 * `routing.locales` reste la liste compilée, elle ne peut pas dépendre d'une
 * donnée modifiable à chaud.
 */
export function AppearanceForm() {
  const t = useTranslations('settings.appearance');
  const tTheme = useTranslations('theme');
  const tLocale = useTranslations('locale');

  const { settings } = useCompanySettings();
  const { theme, setTheme } = useTheme();
  const locale = useLocale();
  const router = useRouter();
  const pathname = usePathname();
  const params = useParams();
  const [, startTransition] = useTransition();

  const mounted = useMounted();
  const enabled = routing.locales.filter((code) => settings.locales.includes(code));

  return (
    <div className="flex flex-col gap-6">
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>{t('locale.title')}</CardTitle>
            <CardDescription>{t('locale.description')}</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {enabled.map((value) => (
              <Button
                key={value}
                type="button"
                variant={value === locale ? 'default' : 'outline'}
                size="sm"
                onClick={() => {
                  if (value === locale) return;
                  startTransition(() => {
                    // @ts-expect-error — `params` est typé par route ; le pathname est ici générique.
                    router.replace({ pathname, params }, { locale: value });
                  });
                }}
              >
                {tLocale(value)}
              </Button>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t('mode.title')}</CardTitle>
            <CardDescription>{t('mode.description')}</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {MODES.map(({ value, icon: Icon }) => (
              <Button
                key={value}
                type="button"
                variant={mounted && theme === value ? 'default' : 'outline'}
                size="sm"
                className="gap-1.5"
                onClick={() => setTheme(value)}
              >
                <Icon className="size-4" />
                {tTheme(value)}
              </Button>
            ))}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t('preview.title')}</CardTitle>
          <CardDescription>{t('preview.description')}</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-wrap gap-2">
            <Button size="sm">{t('preview.primary')}</Button>
            <Button size="sm" variant="secondary">
              {t('preview.secondary')}
            </Button>
            <Button size="sm" variant="outline">
              {t('preview.outline')}
            </Button>
          </div>

          <div className="flex flex-wrap gap-2">
            <CellBadge label="Livré" color="#6AB04C" />
            <CellBadge label="En cours de livraison" color="#4B4B4B" />
            <CellBadge label="Refusé" color="#ED4C67" />
            <CellBadge label="Retourné" color="#D63031" />
          </div>

          <div>
            <p className="text-muted-foreground mb-2 text-xs font-medium tracking-wide uppercase">
              {t('preview.tokens')}
            </p>
            <div className="grid grid-cols-3 gap-2 sm:grid-cols-6">
              {(
                [
                  'bg-primary',
                  'bg-secondary',
                  'bg-accent',
                  'bg-muted',
                  'bg-destructive',
                  'bg-sidebar',
                ] as const
              ).map((token) => (
                <div key={token} className="flex flex-col gap-1">
                  <span className={cn('h-10 rounded-md border', token)} />
                  <span className="text-muted-foreground truncate text-[10px]">
                    {token.replace('bg-', '')}
                  </span>
                </div>
              ))}
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
