'use client';

import { type RouteCandidate, routeTargetsSender, SELECTION_MAX } from '@delivery/shared';
import { useFormatter, useTranslations } from 'next-intl';
import { useCallback, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { CitySelect } from '@/components/city-select';
import { SearchableSelect } from '@/components/searchable-select';
import { CandidatePicker, type CandidateQuery } from '@/features/candidates/candidate-picker';
import { ParcelStatusBadge } from '@/features/parcels/status-badge';
import { routeStopOf } from '@/features/routes/route-stop';
import { addRouteParcelsAction, routeCandidatePageAction } from '@/lib/actions/routes';

const ALL_DISTRICTS = '__all__';

/**
 * « AJOUTER DES COLIS » À UN CIRCUIT — C12 bloc 3, Recette 5 point 6, et le
 * chantier « Plafond de 200 ».
 *
 * La liste vient du serveur par pages, au vrai total (`CandidatePicker`) ; la
 * recherche (code, destinataire, téléphone, ville…) et le filtre de quartier
 * sont faits au serveur, sur TOUTE la liste. « Tout cocher » coche tous les
 * candidats cochables de la recherche et du quartier, chargés ou non ; l'API
 * les relit dans la transaction d'ajout.
 *
 * Sur un ramassage (point 6) : un colis déjà sur un circuit PARTI est grisé avec
 * sa raison ; sur un bon ou un circuit en brouillon, il porte son badge « Sur
 * BR-… / CIR-… » et l'ajout le DÉPLACE.
 */
export function RouteComposer({
  code,
  kind,
  onDone,
}: {
  code: string;
  /** Le TYPE du circuit : il décide de qui s'affiche sur chaque ligne. */
  kind: string;
  onDone: () => void;
}) {
  const t = useTranslations('routes.compose');
  const tPick = useTranslations('candidates');
  const format = useFormatter();
  const targetsSender = routeTargetsSender(kind);

  const [district, setDistrict] = useState(ALL_DISTRICTS);
  const [districts, setDistricts] = useState<Array<{ key: string; name: string }>>([]);
  const districtFilter = district === ALL_DISTRICTS ? undefined : district;
  /*
   * LE FILTRE VILLE — lot F, F2 (D-206). Le circuit propose les colis de
   * toutes les villes du livreur ; on en coche une seule en la choisissant ici.
   * Les villes viennent de toute la liste, comme les quartiers.
   */
  const [city, setCity] = useState(ALL_DISTRICTS);
  const [cities, setCities] = useState<Array<{ id: string; name: string }>>([]);
  const cityFilter = city === ALL_DISTRICTS ? undefined : city;
  const [total, setTotal] = useState(0);

  const load = useCallback(
    async (query: CandidateQuery) => {
      const result = await routeCandidatePageAction(code, {
        ...query,
        district: districtFilter,
        cityId: cityFilter,
      });
      // Les quartiers viennent de TOUTE la liste, pas de la page affichée.
      if (result.ok && !targetsSender) setDistricts(result.data.districts);
      if (result.ok) {
        setCities(result.data.cities);
        setTotal(result.data.total);
      }
      return result;
    },
    [code, cityFilter, districtFilter, targetsSender],
  );

  return (
    <CandidatePicker<RouteCandidate>
      title={t('title')}
      description={kind === 'pickup' ? t('pickupHint') : t('hint')}
      searchPlaceholder={t('searchPlaceholder')}
      load={load}
      filtersKey={`${city}\u0000${district}`}
      toolbar={
        <>
          {cities.length > 1 || city !== ALL_DISTRICTS ? (
            <CitySelect
              cities={cities}
              value={city}
              onValueChange={(next) => {
                // Changer de ville change les quartiers : on repart de tous.
                setDistrict(ALL_DISTRICTS);
                setCity(next);
              }}
              placeholder={t('city')}
              emptyOption={{ value: ALL_DISTRICTS, label: t('allCities') }}
              className="sm:w-52"
              aria-label={t('city')}
            />
          ) : null}
          {districts.length > 0 ? (
            <SearchableSelect
              options={districts.map((entry) => ({ value: entry.key, label: entry.name }))}
              value={district}
              onValueChange={setDistrict}
              placeholder={t('district')}
              emptyOption={{ value: ALL_DISTRICTS, label: t('allDistricts') }}
              className="sm:w-52"
            />
          ) : null}
          {/* Au-delà de SELECTION_MAX, « Tout cocher » serait refusé : le dire avant. */}
          {total > SELECTION_MAX ? (
            <p className="text-warning basis-full text-xs">
              {t('tooMany', { limit: String(SELECTION_MAX) })}
            </p>
          ) : null}
        </>
      }
      lockedReason={(candidate) =>
        candidate.heldBy?.locked
          ? t('lockedReason', { code: candidate.heldBy.routeCode ?? '' })
          : null
      }
      submit={(selection) => addRouteParcelsAction(code, selection, districtFilter, cityFilter)}
      submitLabel={(count) => tPick('addParcels', { count })}
      successMessage={(count) => t('addedCount', { count })}
      onDone={onDone}
      renderItem={(candidate) => {
        const stop = routeStopOf(kind, stopSourceOf(candidate));
        return (
          <div className="flex items-start gap-3">
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-mono text-sm font-medium">{candidate.code}</span>
                <ParcelStatusBadge statusId={candidate.statusId} />
                {!targetsSender && candidate.destinationDistrictName ? (
                  <Badge variant="secondary" className="rounded-full font-normal">
                    {candidate.destinationDistrictName}
                  </Badge>
                ) : null}
                {candidate.heldBy && !candidate.heldBy.locked ? (
                  <Badge variant="outline" className="rounded-full font-normal">
                    {t('heldBy', {
                      where: [candidate.heldBy.noteCode, candidate.heldBy.routeCode]
                        .filter(Boolean)
                        .join(' / '),
                    })}
                  </Badge>
                ) : null}
              </div>
              <p className="text-muted-foreground mt-0.5 truncate text-xs">
                {stop.name} · {stop.phone}
              </p>
              <p className="text-muted-foreground truncate text-xs">{stop.place}</p>
            </div>
            {targetsSender ? null : (
              <span className="shrink-0 text-sm font-medium tabular-nums">
                {format.number(candidate.codToCollect / 100, 'currency')}
              </span>
            )}
          </div>
        );
      }}
    />
  );
}

/**
 * Un candidat vu comme un ARRÊT — `routeStopOf()` lit la forme d'un colis de
 * circuit ; un candidat porte les mêmes faits sous d'autres noms.
 */
function stopSourceOf(candidate: RouteCandidate) {
  return {
    recipientName: candidate.recipientName,
    recipientPhone: candidate.recipientPhone,
    deliveryAddress: candidate.deliveryAddress,
    destinationDistrictName: candidate.destinationDistrictName,
    destinationCityName: null,
    senderName: candidate.clientName,
    senderPhone: candidate.senderPhone,
    pickupAddress: candidate.pickupAddress,
    originCityName: candidate.originCityName,
  };
}
