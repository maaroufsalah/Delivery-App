'use client';

import { DASHBOARD_PERIODS, type DashboardPeriod } from '@delivery/shared';
import { CalendarRange } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useSearchParams } from 'next/navigation';
import { useTransition } from 'react';
import { SearchableSelect } from '@/components/searchable-select';
import { Input } from '@/components/ui/input';
import { usePathname, useRouter } from '@/i18n/navigation';
import { type PeriodMemory, writePeriodPreference } from '@/lib/dashboard-period';

/**
 * LE SÉLECTEUR DE PÉRIODE — chantier « Filtre de période ».
 *
 * Un seul composant pour les DEUX tableaux de bord : le principal et le
 * financier. Le choix vit dans l'URL — un lien se partage, le bouton Retour le
 * ramène — et il est mémorisé en cookie comme dernier choix, lu au rendu
 * serveur pour que le premier pixel soit déjà la bonne période.
 *
 * **Le composant ne calcule aucune borne.** `from` et `to` lui viennent
 * résolus du serveur (fuseau du tenant compris) ; pour « personnalisé » il ne
 * fait que les porter dans l'URL. Recalculer « ce mois » ici aurait fait deux
 * vérités, et celle de l'écran aurait divergé de celle de l'API.
 */
export function PeriodFilter({
  period,
  from,
  to,
  memory = 'dashboard',
}: {
  period: DashboardPeriod;
  /** Bornes RÉSOLUES `AAAA-MM-JJ`, telles que le serveur les a lues. */
  from: string;
  to: string;
  /** L'écran dont on mémorise le dernier choix — point 1 du 26/09. */
  memory?: PeriodMemory;
}) {
  const t = useTranslations('dashboard.period');
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [, startTransition] = useTransition();

  /**
   * Réécrit l'URL en conservant les autres filtres (agence, livreur). La page
   * courante, elle, ne survit pas : sur une liste, changer de période change
   * le nombre de pages, et rester sur la page 4 d'une fenêtre qui n'en a plus
   * que deux rendrait une table vide.
   */
  const push = (mutate: (next: URLSearchParams) => void) => {
    const next = new URLSearchParams(params.toString());
    mutate(next);
    next.delete('page');
    const query = next.toString();
    startTransition(() => {
      // Un filtre ne fait pas remonter la page (chantier « Affichage », point 1).
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    });
  };

  const onPeriod = (value: string) => {
    const chosen = value as DashboardPeriod;

    if (chosen === 'custom') {
      // On amorce le personnalisé avec la fenêtre courante : l'opérateur ajuste
      // à partir de ce qu'il voyait déjà, plutôt que de repartir de vide.
      writePeriodPreference('custom', { from, to }, memory);
      push((next) => {
        next.set('period', 'custom');
        next.set('from', from);
        next.set('to', to);
      });
      return;
    }

    // Les périodes nommées n'ont pas de bornes à porter : la valeur suffit.
    writePeriodPreference(chosen, undefined, memory);
    push((next) => {
      next.set('period', chosen);
      next.delete('from');
      next.delete('to');
    });
  };

  const onFrom = (value: string) => {
    if (!value) return;
    // Début ≤ fin : la fin recule avec le début plutôt que de laisser une
    // fenêtre vide. Le serveur échange aussi, mais l'écran ne doit pas afficher
    // un intervalle absurde le temps de l'aller-retour.
    const nextTo = value > to ? value : to;
    writePeriodPreference('custom', { from: value, to: nextTo }, memory);
    push((next) => {
      next.set('period', 'custom');
      next.set('from', value);
      next.set('to', nextTo);
    });
  };

  const onTo = (value: string) => {
    if (!value) return;
    const nextFrom = value < from ? value : from;
    writePeriodPreference('custom', { from: nextFrom, to: value }, memory);
    push((next) => {
      next.set('period', 'custom');
      next.set('from', nextFrom);
      next.set('to', value);
    });
  };

  return (
    <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
      <SearchableSelect
        options={DASHBOARD_PERIODS.map((value) => ({ value, label: t(value) }))}
        value={period}
        onValueChange={onPeriod}
        className="w-full sm:w-52"
        aria-label={t('label')}
        icon={CalendarRange}
      />

      {period === 'custom' ? (
        <div className="flex items-center gap-2">
          <Input
            type="date"
            value={from}
            max={to}
            aria-label={t('from')}
            className="w-full sm:w-40"
            onChange={(event) => onFrom(event.target.value)}
          />
          <span className="text-muted-foreground text-sm">–</span>
          <Input
            type="date"
            value={to}
            min={from}
            aria-label={t('to')}
            className="w-full sm:w-40"
            onChange={(event) => onTo(event.target.value)}
          />
        </div>
      ) : null}
    </div>
  );
}
