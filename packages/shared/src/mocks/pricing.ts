import type { ClientDiscount, PricingRule } from '../pricing';
import type { AmountInCents } from '../types/common';
import { mockCities, mockZones } from './geography';

/**
 * Matrice tarifaire ville × ville — DOMAIN.md §3.1 (legacy `tarifs`).
 *
 * Elle est ENGENDRÉE à partir des zones plutôt que saisie ligne à ligne : le
 * legacy stockait 400 lignes à la main, la moitié incohérentes entre elles.
 * Quelques corridors majeurs sont surchargés explicitement.
 */

/** Tarif quartier, en centimes, par couple de zones. Clé : `<zone>|<zone>`, ordre indifférent. */
const ZONE_PAIR_RATE: Record<string, AmountInCents> = {
  'zone-1|zone-1': 2500,
  'zone-1|zone-2': 3500,
  'zone-1|zone-3': 4000,
  'zone-2|zone-2': 3000,
  'zone-2|zone-3': 4500,
  'zone-3|zone-3': 3500,
};

/** Corridors à fort volume, négociés sous le tarif de zone. */
const CORRIDOR_OVERRIDES: Record<string, AmountInCents> = {
  'cas|rab': 2500,
  'rab|cas': 2500,
  'cas|moh': 2000,
  'moh|cas': 2000,
  'rab|sal': 2000,
  'sal|rab': 2000,
  'cas|mar': 3500,
  'mar|cas': 3500,
  'cas|tng': 3500,
  'tng|cas': 3500,
  'fes|mek': 2500,
  'mek|fes': 2500,
};

/** Tarif même ville : le legacy le lit sur la zone (`zone_tarifs.meme_ville`). */
function sameCityRate(zoneId: string): AmountInCents {
  return mockZones.find((zone) => zone.id === zoneId)?.sameCityRate ?? 2000;
}

function districtRate(originZone: string, destinationZone: string): AmountInCents {
  return (
    ZONE_PAIR_RATE[`${originZone}|${destinationZone}`] ??
    ZONE_PAIR_RATE[`${destinationZone}|${originZone}`] ??
    4000
  );
}

export const mockPricingRules: PricingRule[] = mockCities.flatMap((origin) =>
  mockCities.map((destination) => {
    const key = `${origin.id}|${destination.id}`;
    const district =
      CORRIDOR_OVERRIDES[key] ??
      (origin.id === destination.id
        ? sameCityRate(origin.zoneId)
        : districtRate(origin.zoneId, destination.zoneId));

    return {
      originCityId: origin.id,
      destinationCityId: destination.id,
      district,
      // Livrer en agence coûte moins cher : pas de dernier kilomètre.
      agency: Math.max(district - 500, 1000),
      relayPoint: Math.max(district - 300, 1200),
    };
  }),
);

/**
 * Remises client — legacy `ClientRemise`. Elle ÉCRASE le tarif de la matrice
 * (DOMAIN.md §3.1 étape 3), elle ne s'y soustrait pas.
 */
export const mockClientDiscounts: ClientDiscount[] = [
  {
    clientId: 'cli-1',
    zoneIds: [],
    flatRate: 2200,
    sameCityRate: 1800,
    isActive: true,
  },
  {
    clientId: 'cli-4',
    zoneIds: ['zone-1'],
    flatRate: 2000,
    sameCityRate: 1500,
    isActive: true,
  },
  {
    clientId: 'cli-7',
    zoneIds: ['zone-3'],
    flatRate: 3000,
    sameCityRate: null,
    isActive: true,
  },
  // Remise désactivée : sert à vérifier que le filtre `isActive` est bien appliqué.
  {
    clientId: 'cli-8',
    zoneIds: [],
    flatRate: 1000,
    sameCityRate: 1000,
    isActive: false,
  },
];
