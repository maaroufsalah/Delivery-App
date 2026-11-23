'use client';

import { Languages } from 'lucide-react';
import { useLocale, useTranslations } from 'next-intl';
import { useParams } from 'next/navigation';
import { useTransition } from 'react';
import { useCompanySettings } from '@/components/theme/company-settings-provider';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { usePathname, useRouter } from '@/i18n/navigation';
import { routing } from '@/i18n/routing';
import { cn } from '@/lib/utils';

/**
 * Change de langue en conservant la page courante. Le sens d'écriture suit
 * automatiquement : `dir` est posé par le layout racine à partir de la locale.
 *
 * Seules les langues activées dans Paramètres > Société sont proposées ;
 * `routing.locales` reste la liste compilée, elle ne peut pas dépendre d'une
 * donnée modifiable à chaud.
 */
export function LocaleSwitcher({ className }: { className?: string }) {
  const t = useTranslations('locale');
  const locale = useLocale();
  const router = useRouter();
  const pathname = usePathname();
  const params = useParams();
  const [isPending, startTransition] = useTransition();
  const { settings } = useCompanySettings();

  const enabled = routing.locales.filter((code) => settings.locales.includes(code));

  // Une seule langue activée : le sélecteur n'a plus rien à offrir.
  if (enabled.length < 2) return null;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className={cn(className)}
          aria-label={t('label')}
          disabled={isPending}
        >
          <Languages className="size-4" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="min-w-36">
        {enabled.map((value) => (
          <DropdownMenuItem
            key={value}
            onSelect={() => {
              if (value === locale) return;
              startTransition(() => {
                router.replace(
                  // @ts-expect-error — `params` est typé par route ; ici le pathname est générique.
                  { pathname, params },
                  { locale: value },
                );
              });
            }}
            className={cn('cursor-pointer', value === locale && 'font-semibold')}
          >
            {t(value)}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
