import { z } from 'zod';
import { isCorrectionTarget } from './status-correction';
import { PARCEL_STATUSES } from './statuses';

/**
 * LA CORRESPONDANCE DES STATUTS D'UN PRESTATAIRE — chantier sync-wimo-speedy,
 * §3 de `docs/chantiers/sync-wimo-speedy-ANALYSE.md`.
 *
 * Wimo, sous-traitant de la société, pousse les statuts de SES livreurs. Les
 * deux applications descendent du même legacy et partagent les identifiants.
 *
 * **Versionnée ici, surchargeable en base.** La table par défaut vit dans le
 * code, avec son numéro de version ; chaque jeton peut la surcharger
 * (`ProviderToken.statusMap`), depuis Paramètres > Prestataires, sans
 * déploiement (D-019). Une cible est un statut que la correction de
 * l'administrateur sait poser (`isCorrectionTarget`, D-250) : jamais un statut
 * qui naît du départ d'un circuit.
 *
 * **SYNCHRO COMPLÈTE — décisions de Salah du 23/11** :
 *  - chaque statut Wimo qui existe chez nous est repris tel quel : les issues
 *    de terrain, `4 Livré` et `13 En cours de livraison` ;
 *  - **tous les retours Wimo** (`5, 20, 21, 22, 23, 24`) deviennent `20 En
 *    attente de retour` : le colis reste au livreur WIMO, et son retour
 *    physique passe par le bon de retour à l'agence, comme pour nos livreurs
 *    (`24` ne se pose chez nous qu'au départ d'un circuit de retour) ;
 *  - **ignorés** (`null`) : les étapes internes de l'agence Wimo (`3, 7, 16,
 *    31` — chez nous le colis reste sur le circuit WIMO) et son transit (`12`),
 *    les statuts sans équivalent (`15, 30, 36`), les statuts initiaux (`17,
 *    18, 38`). Un statut ignoré est journalisé, jamais appliqué ; reçu sur un
 *    colis déjà livré, il lève une alerte.
 */
export const PROVIDER_STATUS_MAP_VERSION = 2;

/**
 * Les 35 statuts « posables » de Wimo (`ColisStatut::posables()` moins `19`),
 * libellés de `Delivery/docs/API_PARTENAIRES.md` §6.
 */
export const PROVIDER_STATUSES: ReadonlyArray<{ id: number; label: string }> = [
  { id: 3, label: 'Colis ramassé' },
  { id: 4, label: 'Livré' },
  { id: 5, label: 'Retourné' },
  { id: 6, label: 'Refusé' },
  { id: 7, label: 'Expédié' },
  { id: 8, label: 'Hors zone' },
  { id: 9, label: 'Injoignable' },
  { id: 10, label: 'Numéro erroné' },
  { id: 11, label: '1er appel pas de réponse' },
  { id: 12, label: 'En cours d’expédition' },
  { id: 13, label: 'En cours de livraison' },
  { id: 14, label: 'Reporté' },
  { id: 15, label: 'En agence destination' },
  { id: 16, label: 'En attente de livraison' },
  { id: 17, label: 'En cours de ramassage' },
  { id: 18, label: 'Ramassé par le livreur' },
  { id: 20, label: 'En attente de retour' },
  { id: 21, label: 'En cours de récupération' },
  { id: 22, label: 'Récupéré par livreur' },
  { id: 23, label: 'Retour récupéré' },
  { id: 24, label: 'En cours de retour' },
  { id: 25, label: 'Boîte vocale' },
  { id: 26, label: 'Client en voyage' },
  { id: 27, label: 'Annulé par le client' },
  { id: 28, label: 'Client déjà livré' },
  { id: 29, label: 'Pas intéressé' },
  { id: 30, label: 'Au guichet Amana' },
  { id: 31, label: 'En stock' },
  { id: 32, label: '2e appel pas de réponse' },
  { id: 33, label: '3e appel pas de réponse' },
  { id: 34, label: '1er appel pas de réponse + SMS' },
  { id: 35, label: '2e appel pas de réponse + SMS' },
  { id: 36, label: '3e appel pas de réponse + SMS' },
  { id: 37, label: 'Envoi erroné (faute destination)' },
  { id: 38, label: 'Programmé' },
];

export const PROVIDER_STATUS_IDS: readonly number[] = PROVIDER_STATUSES.map((status) => status.id);

/** Statut prestataire → statut de la société, ou `null` : ignoré. */
export type ProviderStatusMap = Readonly<Record<number, number | null>>;

/** Repris tels quels : les issues de terrain, Livré, et En cours de livraison. */
const SAME_ID = [4, 6, 8, 9, 10, 11, 13, 14, 25, 26, 27, 28, 29, 32, 33, 34, 35, 37] as const;

/** `20 En attente de retour` : la cible de tous les retours du prestataire. */
export const PROVIDER_RETURN_TARGET = 20;

/** Les retours Wimo, tous ramenés à `20` (décision du 23/11). */
export const PROVIDER_RETURN_STATUS_IDS: readonly number[] = [5, 20, 21, 22, 23, 24];

export const DEFAULT_PROVIDER_STATUS_MAP: ProviderStatusMap = Object.fromEntries(
  PROVIDER_STATUS_IDS.map((id) => {
    if ((SAME_ID as readonly number[]).includes(id)) return [id, id];
    if (PROVIDER_RETURN_STATUS_IDS.includes(id)) return [id, PROVIDER_RETURN_TARGET];
    return [id, null];
  }),
);

/**
 * Les cibles qu'une correspondance peut viser : tout statut que la correction
 * de l'administrateur sait poser (D-250). Les issues de terrain en font partie ;
 * ce qui naît du départ d'un circuit (12, 17, 24) ou de l'historique (5, 21),
 * non.
 */
export function providerStatusTargets(): number[] {
  return PARCEL_STATUSES.map((status) => status.id as number).filter(isCorrectionTarget);
}

/**
 * La surcharge en base, telle qu'on l'écrit : clés en chaîne (JSON), valeurs
 * entières ou `null`. Une clé inconnue ou une cible que la correction ne sait
 * pas poser est refusée.
 */
export const providerStatusMapSchema = z
  .record(z.string(), z.number().int().positive().nullable())
  .superRefine((map, ctx) => {
    const targets = providerStatusTargets();
    for (const [key, value] of Object.entries(map)) {
      if (!PROVIDER_STATUS_IDS.includes(Number(key))) {
        ctx.addIssue({ code: 'custom', path: [key], message: 'unknown-provider-status' });
      }
      if (value !== null && !targets.includes(value)) {
        ctx.addIssue({ code: 'custom', path: [key], message: 'target-not-correctable' });
      }
    }
  });

export type ProviderStatusMapOverride = z.infer<typeof providerStatusMapSchema>;

/**
 * La table effective : le défaut, puis la surcharge. Une surcharge illisible
 * (écrite à la main en base, version future) est ignorée en entier plutôt
 * qu'appliquée à moitié — le défaut ignore tout ce qui est douteux.
 */
export function effectiveProviderStatusMap(override: unknown): ProviderStatusMap {
  const parsed = providerStatusMapSchema.safeParse(override ?? {});
  if (!parsed.success) return DEFAULT_PROVIDER_STATUS_MAP;

  const merged: Record<number, number | null> = { ...DEFAULT_PROVIDER_STATUS_MAP };
  for (const [key, value] of Object.entries(parsed.data)) merged[Number(key)] = value;
  return merged;
}

/** Le statut à appliquer, ou `null` : ignoré (statut inconnu compris). */
export function mapProviderStatus(map: ProviderStatusMap, providerStatus: number): number | null {
  return map[providerStatus] ?? null;
}

/* --- Le contrat de l'endpoint ------------------------------------------- */

/** Le corps de `POST /provider/parcels/:code/status`. */
export const providerStatusSchema = z.object({
  providerStatus: z.number().int().positive(),
  /** Quand le statut a été posé chez le prestataire, avec son fuseau. Sert à l'ordre. */
  occurredAt: z.iso.datetime({ offset: true }),
  comment: z.string().trim().max(255).optional(),
  /** Reporté : la date convenue avec le destinataire. */
  scheduledAt: z.string().trim().optional(),
  /** Livré : ce que le livreur du prestataire a encaissé, en dirhams. Exigé. */
  collectedAmount: z.number().min(0).max(1_000_000).optional(),
  /** Le code du colis chez le prestataire, à titre d'information. */
  providerCode: z.string().trim().max(40).optional(),
});

export type ProviderStatusPayload = z.output<typeof providerStatusSchema>;

export const PROVIDER_MODES = ['LOG_ONLY', 'ACTIVE'] as const;
export type ProviderModeValue = (typeof PROVIDER_MODES)[number];

export const PROVIDER_CALL_OUTCOMES = [
  'APPLIED',
  'WOULD_APPLY',
  'ALREADY_APPLIED',
  'IGNORED',
  'STALE',
  'REJECTED',
  'WOULD_REJECT',
] as const;
export type ProviderCallOutcomeValue = (typeof PROVIDER_CALL_OUTCOMES)[number];

/** Ce que l'endpoint répond. */
export interface ProviderStatusResult {
  outcome: ProviderCallOutcomeValue;
  mode: ProviderModeValue;
  parcelCode: string;
  providerStatus: number;
  mappedStatus: number | null;
  /** Le statut du colis chez nous après l'appel (inchangé hors `APPLIED`). */
  statusId: number | null;
  reason?: string;
}

/* --- Paramètres > Prestataires ------------------------------------------ */

export const providerTokenCreateSchema = z.object({
  name: z.string().trim().min(1, 'required').max(80),
  courierId: z.string().min(1, 'required'),
});
export type ProviderTokenCreatePayload = z.output<typeof providerTokenCreateSchema>;

export const providerTokenModeSchema = z.object({ mode: z.enum(PROVIDER_MODES) });
export type ProviderTokenModePayload = z.output<typeof providerTokenModeSchema>;

export const providerStatusMapUpdateSchema = z.object({ statusMap: providerStatusMapSchema });
export type ProviderStatusMapUpdatePayload = z.output<typeof providerStatusMapUpdateSchema>;

/** Une ligne de la liste des prestataires. Le jeton n'y est jamais. */
export interface ProviderTokenListItem {
  id: string;
  name: string;
  courierId: string;
  courierName: string;
  tokenPrefix: string;
  mode: ProviderModeValue;
  rateLimitPerMinute: number;
  /** La table effective, défaut + surcharge. */
  statusMap: Record<string, number | null>;
  previousExpiresAt: string | null;
  revokedAt: string | null;
  rotatedAt: string | null;
  lastUsedAt: string | null;
  createdAt: string;
  /** Les alertes non traitées de ce prestataire. */
  openAlerts: number;
}

/** Le jeton en clair, rendu UNE fois, à la création ou à la rotation. */
export interface ProviderTokenSecret {
  item: ProviderTokenListItem;
  token: string;
}

export interface ProviderCallListItem {
  id: string;
  at: string;
  eventKey: string | null;
  parcelCode: string;
  providerStatus: number;
  mappedStatus: number | null;
  occurredAt: string | null;
  mode: ProviderModeValue;
  outcome: ProviderCallOutcomeValue;
  httpStatus: number;
  reason: string | null;
  /** Une correction refusée, à reprendre à la main (point 3 du 21/11). */
  alert: boolean;
  alertResolvedAt: string | null;
}
