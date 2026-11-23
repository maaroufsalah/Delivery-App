'use client';

import type { Agency, Courier, DashboardPeriod } from '@delivery/shared';
import { RotateCcw } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useSearchParams } from 'next/navigation';
import { SearchableSelect } from '@/components/searchable-select';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { PeriodFilter } from '@/features/dashboard/period-filter';
import { useRouter } from '@/i18n/navigation';

/** `Select` n'accepte pas la chaîne vide comme valeur. */
const ALL = '__all__';

/**
 * LES FILTRES DU TABLEAU DE BORD FINANCIER — point 10, étendu par le chantier
 * « Filtre de période ».
 *
 * Une seule barre pour toute la page : la période, l'agence, le livreur. La
 * période passe désormais par le sélecteur PARTAGÉ avec le tableau de bord
 * principal — mêmes choix, même mémoire, mêmes bornes résolues par le serveur.
 * Les deux champs de date crus ont disparu : ils vivent dans « personnalisé »,
 * là où ils ont un sens.
 *
 * **Tout passe par l'URL** : un tableau de bord se partage en collant un lien,
 * et un rechargement ne ramène pas « ce mois » quand on regardait juillet.
 */
export function FinanceFilters({
  agencies,
  couriers,
  period,
  from,
  to,
}: {
  agencies: Agency[];
  couriers: Courier[];
  period: DashboardPeriod;
  /** Bornes RÉSOLUES par le serveur : l'écran affiche ce qui a été lu. */
  from: string;
  to: string;
}) {
  const t = useTranslations('finance.overview.filters');
  const router = useRouter();
  const params = useSearchParams();

  const setParam = (key: string, value: string) => {
    const next = new URLSearchParams(params.toString());
    if (value === ALL) next.delete(key);
    else next.set(key, value);
    const query = next.toString();
    // Un filtre ne fait pas remonter la page (chantier « Affichage », point 1).
    router.replace(query ? `/finance?${query}` : '/finance', { scroll: false });
  };

  return (
    <Card className="flex flex-col gap-3 p-4 sm:flex-row sm:flex-wrap sm:items-end">
      <Field label={t('period')}>
        <PeriodFilter period={period} from={from} to={to} />
      </Field>

      {agencies.length > 1 ? (
        <Field label={t('agency')}>
          <SearchableSelect
            options={agencies.map((agency) => ({ value: agency.id, label: agency.name }))}
            value={params.get('agencyId') ?? ALL}
            onValueChange={(v) => setParam('agencyId', v)}
            emptyOption={{ value: ALL, label: t('allAgencies') }}
            className="w-full sm:w-52"
          />
        </Field>
      ) : null}

      <Field label={t('courier')}>
        <SearchableSelect
          options={couriers.map((courier) => ({
            value: courier.id,
            label: `${courier.firstName} ${courier.lastName}`,
          }))}
          value={params.get('courierId') ?? ALL}
          onValueChange={(v) => setParam('courierId', v)}
          emptyOption={{ value: ALL, label: t('allCouriers') }}
          className="w-full sm:w-52"
        />
      </Field>

      <Button variant="ghost" size="sm" onClick={() => router.push('/finance')}>
        <RotateCcw className="size-4" />
        {t('reset')}
      </Button>
    </Card>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label className="text-muted-foreground text-xs">{label}</Label>
      {children}
    </div>
  );
}
