import { findParcelStatus } from './statuses';
import type { JournalValue } from './types/journal';

/**
 * L'HISTORIQUE DES CORRECTIONS D'UN COLIS, LU EN CLAIR — chantier
 * « Historique lisible » (16/11/2026).
 *
 * `field_changes` range ses valeurs en TEXTE, chacune à sa façon : les montants
 * des frais et du COD en centimes (D-109), le chèque reçu en dirhams avec
 * décimales, un client ou une ville par son identifiant, un statut par son
 * numéro, un booléen en « true ». Rien de tout cela ne s'affiche tel quel : un
 * écran lisait « codAmount : 210000 → 175000 » pour « Montant à encaisser :
 * 2 100,00 MAD → 1 750,00 MAD ».
 *
 * **La sorte d'une valeur se lit sur son CHAMP, jamais sur la valeur** : « 200 »
 * vaut 2,00 MAD en centimes et 200,00 MAD en dirhams. Les valeurs stockées ne
 * changent pas ; seule leur lecture est corrigée, lignes anciennes comprises.
 *
 * Chaque champ que l'API peut écrire pour un colis figure ici ;
 * `parcel-trace.spec.ts` (API) le vérifie sur le code qui écrit, et vérifie que
 * chacun a son libellé dans les trois langues.
 */
export type ParcelTraceKind =
  /** Montant en centimes (D-109). */
  | 'cents'
  /** Montant en dirhams, décimales comprises (« 200.00 »). */
  | 'mad'
  | 'number'
  | 'bool'
  | 'date'
  | 'status'
  /** Paiement attendu : `CASH`/`cash`, `CHEQUE`/`cheque`. */
  | 'payment'
  /**
   * Une fiche nommée par l'API (`oldLabel` / `newLabel`), jamais son
   * identifiant. Le suivi d'un retour d'échange range déjà le NOM du livreur
   * (`exchange-return-follow.ts`) : sans nom résolu, un texte qui n'a pas la
   * forme d'un identifiant se lit tel quel.
   */
  | 'named'
  | 'text';

export const PARCEL_TRACE_FIELDS = {
  codAmount: 'cents',
  deliveryFee: 'cents',
  failureFee: 'cents',
  otherFees: 'cents',
  chequePaid: 'mad',
  quantity: 'number',
  recipientName: 'text',
  recipientPhone: 'text',
  deliveryAddress: 'text',
  destinationDistrictName: 'text',
  pickupAddress: 'text',
  destinationDistrictId: 'named',
  destinationCityId: 'named',
  originCityId: 'named',
  clientId: 'named',
  agencyId: 'named',
  courierId: 'named',
  statusId: 'status',
  expectedPayment: 'payment',
  blRequested: 'bool',
  deletedAt: 'date',
  deliveryNote: 'text',
  exchange: 'text',
} as const satisfies Record<string, ParcelTraceKind>;

export type ParcelTraceField = keyof typeof PARCEL_TRACE_FIELDS;

/** Les champs qui désignent une fiche : l'API les nomme (D-257). */
export const PARCEL_TRACE_NAMED_FIELDS: readonly ParcelTraceField[] = (
  Object.keys(PARCEL_TRACE_FIELDS) as ParcelTraceField[]
).filter((field) => PARCEL_TRACE_FIELDS[field] === 'named');

export function isParcelTraceField(field: string): field is ParcelTraceField {
  return Object.hasOwn(PARCEL_TRACE_FIELDS, field);
}

/** Un identifiant de fiche (cuid, `agc-cas`, `usr-…`) : jamais affiché. */
const looksLikeId = (raw: string): boolean => /^c[a-z0-9]{20,}$/.test(raw) || /^[a-z]{2,6}-[a-z0-9-]+$/.test(raw);

const integerCents =(raw: string): number | null => (/^-?\d+$/.test(raw.trim()) ? Number(raw.trim()) : null);
const decimalCents = (raw: string): number | null => {
  const value = Number(raw.trim().replace(',', '.'));
  return raw.trim() !== '' && Number.isFinite(value) ? Math.round(value * 100) : null;
};

/**
 * UNE VALEUR de l'historique, typée pour l'écran comme une valeur du journal
 * (`JournalValue`) : l'écran la dit avec le même crochet que le journal.
 *
 * `label` : le nom que l'API a résolu pour un champ « nommé ». Une fiche
 * introuvable donne `null` (affiché « — »), JAMAIS son identifiant. Une valeur
 * qui ne se lit pas dans sa sorte (ligne très ancienne, saisie étrange) reste
 * du texte, au caractère près : on ne devine pas un montant.
 */
export function parcelTraceValue(field: string, raw: string | null, label?: string | null): JournalValue {
  if (!isParcelTraceField(field)) return raw ? { kind: 'text', text: raw } : null;
  const kind: ParcelTraceKind = PARCEL_TRACE_FIELDS[field];
  if (kind === 'named') {
    if (label) return { kind: 'text', text: label };
    return raw && raw.trim() !== '' && !looksLikeId(raw.trim()) ? { kind: 'text', text: raw } : null;
  }
  if (raw === null || raw.trim() === '') return null;

  switch (kind) {
    case 'cents': {
      const cents = integerCents(raw);
      return cents === null ? { kind: 'text', text: raw } : { kind: 'money', cents };
    }
    case 'mad': {
      const cents = decimalCents(raw);
      return cents === null ? { kind: 'text', text: raw } : { kind: 'money', cents };
    }
    case 'number': {
      const value = Number(raw);
      return Number.isFinite(value) ? { kind: 'number', value } : { kind: 'text', text: raw };
    }
    case 'bool':
      return raw === 'true' || raw === 'false' ? { kind: 'bool', value: raw === 'true' } : { kind: 'text', text: raw };
    case 'date': {
      const date = new Date(raw);
      return Number.isNaN(date.getTime()) ? { kind: 'text', text: raw } : { kind: 'date', iso: date.toISOString() };
    }
    case 'status': {
      const status = findParcelStatus(Number(raw));
      return status ? { kind: 'parcel-status', key: status.key } : { kind: 'text', text: raw };
    }
    case 'payment': {
      const value = raw.trim().toLowerCase();
      return value === 'cash' || value === 'cheque' ? { kind: 'enum', value } : { kind: 'text', text: raw };
    }
    case 'text':
      return { kind: 'text', text: raw };
  }
}
