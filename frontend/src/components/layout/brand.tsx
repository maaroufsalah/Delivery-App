'use client';

import { useCompanySettings } from '@/components/theme/company-settings-provider';
import { Link } from '@/i18n/navigation';
import { cn } from '@/lib/utils';
import { CompanyLogo } from '@/components/layout/company-logo';

/**
 * Logo + nom de la société. Les deux viennent de la configuration
 * (Paramètres > Société), jamais d'une constante.
 *
 * `<img>` et non `next/image` : le logo peut être une data URL issue de
 * l'upload admin, que l'optimiseur d'images ne sait pas traiter.
 */
export function Brand({
  className,
  href = '/dashboard',
  variant = 'default',
  compact = false,
}: {
  className?: string;
  href?: string;
  variant?: 'default' | 'sidebar';
  /** Barre réduite (C10 bloc 5) : le logo reste, le nom s'efface. */
  compact?: boolean;
}) {
  const { settings } = useCompanySettings();

  return (
    <Link
      href={href}
      className={cn(
        'focus-visible:ring-ring flex items-center gap-2.5 rounded-md outline-none focus-visible:ring-2',
        className,
      )}
    >
      <CompanyLogo src={settings.logo} background={settings.logoBackground} />
      {/* Réduit, le nom quitte l'écran mais pas l'accessibilité : le lien doit
          garder un nom pour un lecteur d'écran. */}
      <span
        className={cn(
          'truncate text-sm font-semibold tracking-tight',
          compact && 'sr-only',
          variant === 'sidebar' ? 'text-sidebar-foreground' : 'text-foreground',
        )}
      >
        {settings.name}
      </span>
    </Link>
  );
}
