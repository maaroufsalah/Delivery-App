import type { DeliveryNote } from '../types/note';
import type { Route } from '../types/route';

/**
 * 5 circuits et 3 bons.
 *
 * Les codes sont séquentiels et lisibles : le legacy générait des
 * `strtoupper(Str::random(10))` (DOMAIN.md §3.2), illisibles et non triables.
 */

export const mockRoutes: Route[] = [
  {
    id: 'rte-1',
    code: 'CIR-2609-001',
    status: 'delivering',
    statusAt: '2026-09-10T08:15:00+01:00',
    kind: 'delivery',
    cityId: 'cas',
    originCityId: 'cas',
    courierId: 'crr-1',
    parcelIds: ['prc-010', 'prc-013', 'prc-028', 'prc-029'],
    exitDate: '2026-09-10T08:00:00+01:00',
    createdAt: '2026-09-09T18:40:00+01:00',
  },
  {
    id: 'rte-2',
    code: 'CIR-2609-002',
    status: 'delivering',
    statusAt: '2026-09-10T09:05:00+01:00',
    kind: 'delivery',
    cityId: 'rab',
    originCityId: 'rab',
    courierId: 'crr-3',
    parcelIds: ['prc-011', 'prc-027'],
    exitDate: '2026-09-10T08:50:00+01:00',
    createdAt: '2026-09-09T19:10:00+01:00',
  },
  {
    id: 'rte-3',
    code: 'CIR-2609-003',
    status: 'dispatched',
    statusAt: '2026-09-11T07:30:00+01:00',
    kind: 'delivery',
    cityId: 'mar',
    originCityId: 'mar',
    courierId: 'crr-4',
    parcelIds: ['prc-012'],
    exitDate: null,
    createdAt: '2026-09-10T20:05:00+01:00',
  },
  {
    id: 'rte-4',
    code: 'CIR-2609-004',
    status: 'draft',
    statusAt: '2026-09-11T06:45:00+01:00',
    kind: 'pickup',
    cityId: 'tng',
    originCityId: 'tng',
    courierId: 'crr-5',
    parcelIds: ['prc-017'],
    exitDate: null,
    createdAt: '2026-09-11T06:45:00+01:00',
  },
  {
    id: 'rte-5',
    code: 'CIR-2609-005',
    status: 'closed',
    statusAt: '2026-09-05T17:20:00+01:00',
    kind: 'delivery',
    cityId: 'aga',
    originCityId: 'aga',
    courierId: 'crr-6',
    parcelIds: ['prc-007', 'prc-015'],
    exitDate: '2026-09-04T08:10:00+01:00',
    createdAt: '2026-09-03T19:00:00+01:00',
  },
];

/** Colis confirmés par le livreur, par circuit (pivot legacy `boxe_colis.confirmed`). */
export const mockRouteConfirmations: Record<string, string[]> = {
  'rte-1': ['prc-010', 'prc-013'],
  'rte-2': ['prc-011'],
  'rte-3': [],
  'rte-4': [],
  'rte-5': ['prc-007', 'prc-015'],
};

export const mockDeliveryNotes: DeliveryNote[] = [
  {
    id: 'bln-1',
    code: 'BR-2609-0001',
    kind: 'pickup',
    status: 'validated',
    clientId: 'cli-1',
    agencyId: 'agc-cas',
    locationCityId: 'cas',
    parcelIds: ['prc-001', 'prc-004', 'prc-010', 'prc-016', 'prc-023', 'prc-028'],
    createdAt: '2026-09-01T07:50:00+01:00',
    validatedAt: '2026-09-01T09:30:00+01:00',
  },
  {
    id: 'bln-2',
    code: 'BD-2609-0002',
    kind: 'dropoff',
    status: 'validated',
    clientId: 'cli-4',
    agencyId: 'agc-cas',
    locationCityId: 'cas',
    parcelIds: ['prc-002', 'prc-009', 'prc-013', 'prc-021', 'prc-024'],
    createdAt: '2026-09-05T10:15:00+01:00',
    validatedAt: '2026-09-05T11:00:00+01:00',
  },
  {
    id: 'bln-3',
    code: 'BR-2609-0003',
    kind: 'pickup',
    status: 'awaiting-validation',
    clientId: 'cli-6',
    agencyId: 'agc-tng',
    locationCityId: 'tng',
    parcelIds: ['prc-008', 'prc-017', 'prc-026'],
    createdAt: '2026-09-11T08:05:00+01:00',
    validatedAt: null,
  },
];
