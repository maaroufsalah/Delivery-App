'use client';

import type { ParcelStatusGroup } from '@delivery/shared';
import {
  CheckCircle2,
  CircleAlert,
  MapPin,
  PackageSearch,
  Search,
  Truck,
  Undo2,
} from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { useRouter } from '@/i18n/navigation';
import type { PublicTracking } from '@/lib/api/public';

/**
 * Suivi public d'un colis.
 *
 * Volontairement pauvre. Les 34 statuts internes sont repliés sur leur FAMILLE
 * côté API : le destinataire voit « livraison en cours », pas « 2e appel sans
 * réponse ». Aucune note interne, aucun auteur, aucun montant.
 */

/** Une icône et une couleur par famille — les seules qui sortent au public. */
const GROUP_STYLE: Record<ParcelStatusGroup, { icon: typeof Truck; color: string }> = {
  initial: { icon: PackageSearch, color: '#D35400' },
  transit: { icon: Truck, color: '#2F3640' },
  pivot: { icon: MapPin, color: '#FFA801' },
  retry: { icon: CircleAlert, color: '#8C7AE6' },
  failure: { icon: CircleAlert, color: '#ED4C67' },
  return: { icon: Undo2, color: '#D63031' },
  terminal: { icon: CheckCircle2, color: '#6AB04C' },
};

export function TrackingSearch({ defaultCode = '' }: { defaultCode?: string }) {
  const t = useTranslations('public.track');
  const router = useRouter();
  const [code, setCode] = useState(defaultCode);

  return (
    <form
      className="flex flex-col gap-2 sm:flex-row"
      onSubmit={(event) => {
        event.preventDefault();
        const trimmed = code.trim();
        if (trimmed) router.push(`/track/${encodeURIComponent(trimmed)}`);
      }}
    >
      <Input
        value={code}
        onChange={(event) => setCode(event.target.value.toUpperCase())}
        placeholder="CAS010012609CAS"
        aria-label={t('inputLabel')}
        className="font-mono"
        dir="ltr"
        spellCheck={false}
      />
      <Button type="submit" className="shrink-0 gap-1.5">
        <Search className="size-4" aria-hidden />
        {t('action')}
      </Button>
    </form>
  );
}

export function TrackingResult({ tracking }: { tracking: PublicTracking }) {
  const t = useTranslations('public.track');
  const tGroup = useTranslations('public.track.groups');
  const format = useFormatter();

  const current = GROUP_STYLE[tracking.group];
  const CurrentIcon = current.icon;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex flex-wrap items-center justify-between gap-3">
          <span className="font-mono text-base">{tracking.code}</span>
          <Badge
            className="rounded-full font-normal"
            style={{
              backgroundColor: `color-mix(in oklab, ${current.color} 16%, transparent)`,
              color: current.color,
              borderColor: `color-mix(in oklab, ${current.color} 34%, transparent)`,
            }}
          >
            <CurrentIcon className="me-1.5 size-3.5" aria-hidden />
            {tGroup(tracking.group)}
          </Badge>
        </CardTitle>
      </CardHeader>

      <CardContent className="flex flex-col gap-5">
        <p className="text-muted-foreground text-sm">
          {t('destination', { city: tracking.destinationCityName })}
        </p>

        {tracking.steps.length === 0 ? (
          <p className="text-muted-foreground text-sm">{t('noSteps')}</p>
        ) : (
          <ol className="flex flex-col">
            {/* Du plus récent au plus ancien : ce que le destinataire veut lire
                en premier, c'est où en est son colis maintenant. */}
            {[...tracking.steps].reverse().map((step, index, list) => {
              const style = GROUP_STYLE[step.group];
              const Icon = style.icon;
              const last = index === list.length - 1;

              return (
                <li key={`${step.group}-${step.at}`} className="flex gap-3">
                  <div className="flex flex-col items-center">
                    <span
                      className="flex size-8 shrink-0 items-center justify-center rounded-full border"
                      style={{
                        backgroundColor: `color-mix(in oklab, ${style.color} 14%, transparent)`,
                        borderColor: `color-mix(in oklab, ${style.color} 32%, transparent)`,
                        color: style.color,
                      }}
                    >
                      <Icon className="size-4" aria-hidden />
                    </span>
                    {last ? null : <span className="bg-border w-px flex-1" aria-hidden />}
                  </div>

                  <div className={last ? 'min-w-0 flex-1 pb-1' : 'min-w-0 flex-1 pb-6'}>
                    <p className="text-sm font-medium">{tGroup(step.group)}</p>
                    <p className="text-muted-foreground text-xs tabular-nums">
                      {format.dateTime(new Date(step.at), 'stamp')}
                      {step.cityName ? ` · ${step.cityName}` : ''}
                    </p>
                  </div>
                </li>
              );
            })}
          </ol>
        )}

        <p className="bg-muted/60 text-muted-foreground rounded-md px-3 py-2 text-xs">
          {t('privacyNote')}
        </p>
      </CardContent>
    </Card>
  );
}
