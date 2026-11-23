'use client';

import type { Agency, City, Client, Courier, DashboardPeriod } from '@delivery/shared';
import { Building2, RotateCcw, Store, Truck } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useSearchParams } from 'next/navigation';
import { useTransition } from 'react';
import { CitySelect } from '@/components/city-select';
import { SearchableSelect } from '@/components/searchable-select';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { PeriodFilter } from '@/features/dashboard/period-filter';
import { usePathname, useRouter } from '@/i18n/navigation';
import {
  DASHBOARD_FILTER_KEYS,
  type DashboardFilterKey,
  type DashboardFilterValues,
  writeDashboardFilterPreference,
} from '@/lib/dashboard-period';

/** `Select` n'accepte pas la chaîne vide comme valeur. */
const ALL = '__all__';

/**
 * LA BARRE DE FILTRES GLOBALE — chantier « Dashboard de pilotage », spec § 1.
 *
 * Une seule barre pour toutes les sections : la période (le sélecteur partagé,
 * qui a reçu Hier / 7 j / 30 j / Mois précédent / Cette année), puis Agence,
 * Ville, Livreur, Client, et « Réinitialiser ». Elle remplace les deux
 * sélecteurs qui vivaient dans l'en-tête ; aucune valeur n'est calculée ici.
 *
 * **Tout passe par l'URL** — un tableau de bord se partage en collant un lien,
 * et le bouton Retour ramène le filtre précédent — et l'état complet est
 * MÉMORISÉ en cookie à chaque changement (`dp.dashfilters`), lu au rendu
 * serveur pour que le premier pixel soit déjà le bon. Retirer un filtre
 * l'efface aussi de la mémoire.
 *
 * La page courante ne survit pas à un changement : les tableaux paginés
 * (livreurs, villes, clients) repartent de la première page.
 */
export function DashboardFilters({
  agencies,
  cities,
  couriers,
  clients,
  period,
  from,
  to,
  values,
}: {
  agencies: Agency[];
  cities: City[];
  couriers: Courier[];
  clients: Client[];
  period: DashboardPeriod;
  /** Bornes RÉSOLUES par le serveur : l'écran affiche ce qui a été lu. */
  from: string;
  to: string;
  /** Les filtres EFFECTIFS (URL, sinon mémoire), tels que la page les a lus. */
  values: DashboardFilterValues;
}) {
  const t = useTranslations('dashboard.filters');
  const tAgency = useTranslations('dashboard.agency');
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [, startTransition] = useTransition();

  const commit = (next: DashboardFilterValues) => {
    const search = new URLSearchParams(params.toString());
    for (const key of DASHBOARD_FILTER_KEYS) {
      if (next[key]) search.set(key, next[key]);
      else search.delete(key);
    }
    for (const paged of ['page', 'cpage', 'vpage', 'kpage']) search.delete(paged);
    writeDashboardFilterPreference(next);
    const query = search.toString();
    // Un filtre ne fait pas remonter la page (chantier « Affichage », point 1).
    startTransition(() =>
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false }),
    );
  };

  const set = (key: DashboardFilterKey, value: string) =>
    commit({ ...values, [key]: value === ALL ? undefined : value });

  const reset = () => {
    writeDashboardFilterPreference({});
    const search = new URLSearchParams(params.toString());
    for (const key of [...DASHBOARD_FILTER_KEYS, 'page', 'cpage', 'vpage', 'kpage', 'range']) {
      search.delete(key);
    }
    // La période n'est pas un filtre à « réinitialiser » : elle a sa propre
    // mémoire et son propre défaut ; on la laisse où elle est.
    const query = search.toString();
    startTransition(() =>
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false }),
    );
  };

  const active = DASHBOARD_FILTER_KEYS.filter((key) => values[key]).length;

  return (
    <Card className="flex flex-col gap-3 p-4 print:hidden">
      <div className="flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-end">
        <Field label={t('period')}>
          <PeriodFilter period={period} from={from} to={to} />
        </Field>

        {agencies.length > 1 ? (
          <Field label={tAgency('label')}>
            <SearchableSelect
              options={agencies.map((agency) => ({ value: agency.id, label: agency.name }))}
              value={values.agencyId ?? ALL}
              onValueChange={(v) => set('agencyId', v)}
              emptyOption={{ value: ALL, label: tAgency('all') }}
              className="w-full sm:w-48"
              aria-label={tAgency('label')}
              icon={Building2}
            />
          </Field>
        ) : null}

        {cities.length > 0 ? (
          <Field label={t('city')}>
            <CitySelect
              cities={cities}
              value={values.cityId ?? ALL}
              onValueChange={(v) => set('cityId', v)}
              emptyOption={{ value: ALL, label: t('allCities') }}
              className="w-full sm:w-48"
              aria-label={t('city')}
            />
          </Field>
        ) : null}

        {couriers.length > 0 ? (
          <Field label={t('courier')}>
            <SearchableSelect
              options={couriers.map((courier) => ({
                value: courier.id,
                label: `${courier.firstName} ${courier.lastName}`,
              }))}
              value={values.courierId ?? ALL}
              onValueChange={(v) => set('courierId', v)}
              emptyOption={{ value: ALL, label: t('allCouriers') }}
              className="w-full sm:w-48"
              aria-label={t('courier')}
              icon={Truck}
            />
          </Field>
        ) : null}

        {clients.length > 0 ? (
          <Field label={t('client')}>
            <SearchableSelect
              options={clients.map((client) => ({ value: client.id, label: client.name }))}
              value={values.clientId ?? ALL}
              onValueChange={(v) => set('clientId', v)}
              emptyOption={{ value: ALL, label: t('allClients') }}
              className="w-full sm:w-52"
              aria-label={t('client')}
              icon={Store}
            />
          </Field>
        ) : null}

        <div className="flex items-center gap-2 sm:ms-auto">
          {active > 0 ? (
            <Badge variant="secondary" className="rounded-full tabular-nums">
              {t('active', { count: active })}
            </Badge>
          ) : null}
          <Button
            variant="ghost"
            size="sm"
            onClick={reset}
            disabled={active === 0 && !params.get('range')}
          >
            <RotateCcw className="size-4" aria-hidden />
            {t('reset')}
          </Button>
        </div>
      </div>
    </Card>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <Label className="text-muted-foreground text-xs">{label}</Label>
      {children}
    </div>
  );
}
