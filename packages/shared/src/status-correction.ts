import { z } from 'zod';
import { PARCEL_STATUSES } from './statuses';

/**
 * LA CORRECTION LIBRE DU STATUT PAR L'ADMINISTRATEUR — D-250 (Badr, 13/11/2026),
 * `docs/chantiers/correction-statut-admin.md`.
 *
 * Les livreurs et l'agence font des erreurs de saisie ; l'administrateur
 * corrige un colis vers N'IMPORTE QUEL statut, en un geste. Ce n'est pas la
 * machine à états (`TRANSITIONS`, inchangée) : c'est un geste à part, qui
 * ALIGNE les rattachements du colis (bon, circuit, livreur, bon de retour) sur
 * la famille du statut choisi, et refuse tout ce qui toucherait de l'argent
 * déjà parti.
 *
 * La règle tient en familles, pas en couples de statuts : 35 × 34 passages ne
 * se testent pas, six familles si.
 */

export const CORRECTION_FAMILIES = [
  'before-pickup',
  'at-agency',
  'delivery',
  'return',
  'delivered',
  'route-born',
] as const;

export type CorrectionFamily = (typeof CORRECTION_FAMILIES)[number];

/**
 * Les statuts par famille. Chaque statut du référentiel est dans UNE famille
 * (`status-correction.spec.ts` le vérifie) : un statut ajouté demain sans
 * famille casse le test, pas la production.
 *
 * - `before-pickup` : la marchandise n'est pas chez nous (attente de
 *   ramassage, de dépôt, programmé, annulé par le vendeur) ;
 * - `at-agency` : prise en charge, à l'agence ou en acheminement sans circuit ;
 * - `delivery` : en cours de livraison et toutes les issues de terrain ;
 * - `return` : le flux retour que l'on peut constater (20, 22, 23) ;
 * - `delivered` : livré ;
 * - `route-born` : ce que seul le DÉPART d'un circuit pose (12, 17, 24), ou
 *   l'historique legacy (21, 5). Acceptés au départ, jamais à l'arrivée.
 */
export const CORRECTION_FAMILY_STATUS_IDS: Readonly<Record<CorrectionFamily, readonly number[]>> = {
  'before-pickup': [1, 2, 38, 19],
  'at-agency': [3, 16, 31, 7, 18],
  delivery: [13, 6, 8, 9, 10, 11, 14, 25, 26, 27, 28, 29, 32, 33, 34, 35, 37],
  return: [20, 22, 23],
  delivered: [4],
  'route-born': [12, 17, 24, 21, 5],
};

const FAMILY_OF: ReadonlyMap<number, CorrectionFamily> = new Map(
  CORRECTION_FAMILIES.flatMap((family) =>
    CORRECTION_FAMILY_STATUS_IDS[family].map((id) => [id, family] as const),
  ),
);

/** La famille d'un statut, `null` s'il n'appartient pas au référentiel. */
export function correctionFamilyOf(statusId: number): CorrectionFamily | null {
  return FAMILY_OF.get(statusId) ?? null;
}

/**
 * Peut-on corriger VERS ce statut ? Tous, sauf ceux qui naissent du départ
 * d'un circuit ou de l'historique : sans leur circuit, ils cassent ST-03.
 */
export function isCorrectionTarget(statusId: number): boolean {
  const family = correctionFamilyOf(statusId);
  return family !== null && family !== 'route-born';
}

/** Les statuts de la fenêtre, famille par famille, dans l'ordre du référentiel. */
export function correctionStatusGroups(): Array<{ family: CorrectionFamily; statusIds: number[] }> {
  return CORRECTION_FAMILIES.map((family) => ({
    family,
    statusIds: PARCEL_STATUSES.map((status) => status.id as number).filter(
      (id) => correctionFamilyOf(id) === family,
    ),
  }));
}

/**
 * Le geste : la cible, le MOTIF (obligatoire, seul le vide est refusé), et,
 * pour une cible de livraison, le livreur et le circuit choisis (sinon ceux
 * d'avant). `fingerprint` est celle de l'aperçu : l'écriture refuse si le
 * plan a changé entre-temps.
 */
export const statusCorrectionSchema = z.object({
  statusId: z.coerce.number().int().positive({ error: 'required' }),
  reason: z.string().trim().min(1, 'required').max(500, 'too-long'),
  courierId: z.string().trim().optional(),
  routeCode: z.string().trim().optional(),
  /** Reporté (14) : la date convenue, facultative. */
  scheduledDate: z.string().optional(),
  fingerprint: z.string().trim().optional(),
});

export type StatusCorrectionInput = z.input<typeof statusCorrectionSchema>;
export type StatusCorrectionPayload = z.output<typeof statusCorrectionSchema>;

/**
 * UNE LIGNE DE L'APERÇU — ce que la correction va faire, dans l'ordre. L'écran
 * la traduit (`parcels.correction.step.<kind>`) ; le journal la garde telle
 * quelle.
 */
export type StatusCorrectionStep =
  | {
      kind: 'cancel-delivery';
      routeCode: string | null;
      routeReopened: boolean;
      remittanceCode: string | null;
    }
  | { kind: 'leave-note'; code: string }
  | { kind: 'leave-route'; code: string }
  | { kind: 'leave-handover'; code: string }
  | { kind: 'release-handover'; code: string }
  | { kind: 'mark-route'; code: string }
  | { kind: 'mark-note'; code: string }
  | { kind: 'reuse-line'; routeCode: string; courierName: string }
  | { kind: 'place-on-route'; routeCode: string; courierName: string }
  | { kind: 'open-route'; courierName: string }
  | { kind: 'confirm-outcome'; routeCode: string; statusId: number }
  | { kind: 'deliver'; routeCode: string; amount: number }
  | { kind: 'set-status'; from: number; to: number }
  | { kind: 'failure-flag'; before: boolean; after: boolean }
  | { kind: 'failure-fee'; before: number | null; after: number | null }
  | { kind: 'leave-invoice-draft'; code: string }
  | { kind: 'courier-kept'; courierName: string };

/** Un livreur que la fenêtre propose : ceux de la ville du colis d'abord. */
export interface StatusCorrectionCourierOption {
  id: string;
  name: string;
  coversCity: boolean;
}

/** Un circuit de livraison ouvert du livreur choisi. */
export interface StatusCorrectionRouteOption {
  code: string;
  status: string;
  cityName: string;
}

export interface StatusCorrectionPreview {
  code: string;
  from: number;
  to: number;
  family: CorrectionFamily;
  /** Le livreur retenu (cible de livraison), `null` sinon ou s'il reste à choisir. */
  courierId: string | null;
  /** Le circuit retenu, `null` s'il va être ouvert pour le livreur. */
  routeCode: string | null;
  /** Vrai quand une cible de livraison attend que l'admin choisisse un livreur. */
  needsCourier: boolean;
  couriers: StatusCorrectionCourierOption[];
  routes: StatusCorrectionRouteOption[];
  steps: StatusCorrectionStep[];
  fingerprint: string;
}

export interface StatusCorrectionResult {
  code: string;
  from: number;
  to: number;
  steps: StatusCorrectionStep[];
}
