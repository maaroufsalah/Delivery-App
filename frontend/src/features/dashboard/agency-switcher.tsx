'use client';

import type { Agency } from '@delivery/shared';
import { Building2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useSearchParams } from 'next/navigation';
import { useTransition } from 'react';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { usePathname, useRouter } from '@/i18n/navigation';

/** Valeur sentinelle : Radix Select interdit une SelectItem de valeur vide. */
const ALL_AGENCIES = '__all__';

/**
 * Sélecteur d'agence du tableau de bord.
 *
 * Masqué quand l'installation n'a qu'une agence : un select à une entrée est
 * du bruit. L'agence vit dans l'URL, donc le tableau de bord reste partageable
 * et la sélection survit à un rechargement — c'est déjà la forme du futur
 * `GET /dashboard?agencyId=…`.
 */
export function AgencySwitcher({ agencies }: { agencies: Agency[] }) {
  const t = useTranslations('dashboard.agency');
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [, startTransition] = useTransition();

  if (agencies.length < 2) return null;

  const current = searchParams.get('agencyId') ?? ALL_AGENCIES;

  function onChange(value: string) {
    const next = new URLSearchParams(searchParams.toString());
    if (value === ALL_AGENCIES) {
      next.delete('agencyId');
    } else {
      next.set('agencyId', value);
    }
    const query = next.toString();
    startTransition(() => {
      // Un filtre ne fait pas remonter la page (chantier « Affichage », point 1).
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    });
  }

  return (
    <Select value={current} onValueChange={onChange}>
      <SelectTrigger className="w-full sm:w-60" aria-label={t('label')}>
        <Building2 className="text-muted-foreground size-4 shrink-0" aria-hidden />
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={ALL_AGENCIES}>{t('all')}</SelectItem>
        {agencies.map((agency) => (
          <SelectItem key={agency.id} value={agency.id}>
            {agency.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
