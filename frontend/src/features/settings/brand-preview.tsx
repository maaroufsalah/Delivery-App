'use client';

import { buildThemeTokens, readableForeground } from '@delivery/shared';
import { useTranslations } from 'next-intl';
import { CellBadge } from '@/components/data-table/cells';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { CompanyLogo } from '@/components/layout/company-logo';

/**
 * Aperçu de la marque. Deux niveaux :
 *  - la vignette ci-dessous, isolée, qui montre les couleurs saisies même si
 *    l'aperçu global n'est pas encore appliqué ;
 *  - le reste de la page, qui bascule déjà via `previewBrand`.
 */
const TOKEN_SWATCHES = [
  'bg-primary',
  'bg-secondary',
  'bg-accent',
  'bg-muted',
  'bg-destructive',
  'bg-sidebar',
] as const;

export function BrandPreview({
  name,
  logo,
  logoBackground,
  primaryColor,
  secondaryColor,
}: {
  name: string;
  logo: string;
  logoBackground: string | null;
  primaryColor: string;
  secondaryColor: string;
}) {
  const t = useTranslations('settings.company.preview');
  const tokens = buildThemeTokens({ primary: primaryColor, secondary: secondaryColor });

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('title')}</CardTitle>
        <CardDescription>{t('description')}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-5">
        {/* Vignette d'en-tête : ce que verra l'utilisateur dans la sidebar. */}
        <div
          className="flex items-center gap-3 rounded-lg p-4"
          style={{ backgroundColor: secondaryColor, color: readableForeground(secondaryColor) }}
        >
          <CompanyLogo src={logo} background={logoBackground} />
          <span className="truncate text-sm font-semibold">{name}</span>
          <span
            className="ms-auto rounded-md px-3 py-1.5 text-xs font-medium"
            style={{ backgroundColor: primaryColor, color: readableForeground(primaryColor) }}
          >
            {t('cta')}
          </span>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button size="sm" type="button">
            {t('primary')}
          </Button>
          <Button size="sm" type="button" variant="secondary">
            {t('secondary')}
          </Button>
          <Button size="sm" type="button" variant="outline">
            {t('outline')}
          </Button>
          <Badge className="rounded-full">{t('badge')}</Badge>
        </div>

        <div className="flex flex-wrap gap-2">
          <CellBadge label="Livré" color="#6AB04C" />
          <CellBadge label="En cours de livraison" color="#4B4B4B" />
          <CellBadge label="Refusé" color="#ED4C67" />
        </div>

        <div>
          <p className="text-muted-foreground mb-2 text-xs font-medium tracking-wide uppercase">
            {t('tokens')}
          </p>
          <div className="grid grid-cols-3 gap-2 sm:grid-cols-6">
            {TOKEN_SWATCHES.map((token) => (
              <div key={token} className="flex flex-col gap-1">
                <span className={`h-10 rounded-md border ${token}`} />
                <span className="text-muted-foreground truncate text-[10px]">
                  {token.replace('bg-', '')}
                </span>
              </div>
            ))}
          </div>
          <p className="text-muted-foreground mt-2 font-mono text-[11px]">
            --primary: {tokens.light.primary} · --sidebar: {tokens.light.sidebar}
          </p>
        </div>
      </CardContent>
    </Card>
  );
}
