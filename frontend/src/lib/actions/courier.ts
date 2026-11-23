'use server';

import { DELIVERED } from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import { hasLocale } from 'next-intl';
import { getTranslations } from 'next-intl/server';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import { closeCourierRemittance, reportCourierOutcome } from '@/lib/api/courier';
import { getParcel } from '@/lib/api/parcels';
import { routing } from '@/i18n/routing';

/**
 * Les écritures de l'espace livreur — L1 bloc 3.
 *
 * `src/lib/api/courier.ts` reste le contrat d'endpoint ; cette action ne fait
 * que l'exposer au client et invalider les routes qui en dépendent.
 *
 * **Quatre routes invalidées, et pas une de plus** : la fiche qui vient de
 * changer, la liste qui l'affiche, le circuit qui la porte, et l'accueil dont
 * les chiffres se déduisent de la tournée. Oublier l'accueil laisserait « 3 à
 * livrer » après une livraison — le genre d'écart qu'on attribue au réseau
 * pendant deux jours.
 *
 * `/me/circuits` remplace `/me/tour` depuis L2 bloc 9 : le chemin a changé, la
 * raison de l'invalider non.
 */
export async function reportOutcomeAction(
  code: string,
  input: { statusId: number; comment?: string; collectedAmount?: number; scheduledAt?: string },
): Promise<ReferentialResult> {
  try {
    await reportCourierOutcome(code, input);
  } catch (error) {
    return toFailure(error);
  }

  revalidateCourierParcels([code]);

  return { ok: true, data: undefined };
}

function revalidateCourierParcels(codes: readonly string[]) {
  revalidatePath('/[locale]/(courier)/me', 'page');
  revalidatePath('/[locale]/(courier)/me/circuits', 'page');
  revalidatePath('/[locale]/(courier)/me/circuits/[code]', 'page');
  revalidatePath('/[locale]/(courier)/me/parcels', 'page');
  for (const code of codes) revalidatePath(`/[locale]/(courier)/me/parcels/${code}`, 'page');
}

/** Ce que la sélection a donné, colis par colis. */
export interface BulkOutcomeResult {
  succeeded: string[];
  refused: Array<{ code: string; error: string }>;
}

/** Colis envoyés en même temps à l'API : assez pour aller vite, pas assez pour l'étouffer. */
const BULK_CONCURRENCY = 4;

/**
 * LA MÊME ISSUE POUR LES COLIS COCHÉS DE « MES COLIS » — 21/11/2026.
 *
 * **La route UNITAIRE du mobile, répétée**, et non `POST /routes/:code/outcomes` :
 * celle-ci porte sur UN circuit, refuse tout le lot au premier colis en
 * défaut, et plafonne à `SELECTION_MAX`. « Mes colis » mêle plusieurs circuits,
 * la mission veut « X réussis, Y refusés (motif) » et aucun plafond. Chaque
 * colis passe donc par `POST /courier/parcels/:code/status` — les mêmes
 * contrôles, le même encaissement, le même journal qu'à l'unité.
 *
 * **« Livré » = espèces, COD complet** : aucun montant n'est envoyé, le
 * serveur écrit le dû (`collectedAmountFor`, DOMAIN.md §3.8) — exactement ce
 * que fait la masse du mobile. Un colis attendu en chèque est REFUSÉ ici, même
 * si l'écran l'a déjà écarté : un écran n'est pas un rempart.
 */
export async function reportOutcomesAction(
  codes: string[],
  input: { statusId: number; comment?: string; scheduledAt?: string },
  locale: string,
): Promise<ReferentialResult<BulkOutcomeResult>> {
  // La locale vient de l'écran : `next/root-params` n'existe pas dans une
  // Server Action (voir `i18n/request.ts`).
  const t = await getTranslations({
    locale: hasLocale(routing.locales, locale) ? locale : routing.defaultLocale,
    namespace: 'courier.parcels',
  });
  const unique = [...new Set(codes)];
  const result: BulkOutcomeResult = { succeeded: [], refused: [] };

  const one = async (code: string) => {
    try {
      if (input.statusId === DELIVERED) {
        const parcel = await getParcel(code);
        if (parcel.expectedPayment === 'cheque') {
          result.refused.push({ code, error: t('chequeOneByOne') });
          return;
        }
      }
      await reportCourierOutcome(code, input);
      result.succeeded.push(code);
    } catch (error) {
      result.refused.push({ code, error: toFailure(error).error });
    }
  };

  for (let start = 0; start < unique.length; start += BULK_CONCURRENCY) {
    await Promise.all(unique.slice(start, start + BULK_CONCURRENCY).map(one));
  }

  if (result.succeeded.length > 0) revalidateCourierParcels(result.succeeded);

  return { ok: true, data: result };
}

/**
 * « Clôturer ma caisse » — D-243.
 *
 * Aucun argument : la portée désigne la caisse, l'écran ne passe ni code ni
 * identifiant. Le motif d'un refus (`remittance-empty`, `no-open-remittance`,
 * `remittance-already-closed`) remonte tel quel, l'écran le traduit.
 *
 * L'écran des caisses se relit (la caisse quitte « ce que je porte » pour
 * « caisses fermées »), et l'accueil comme la paie avec lui, qui en comptent.
 */
export async function closeCourierRemittanceAction(): Promise<ReferentialResult> {
  try {
    await closeCourierRemittance();
  } catch (error) {
    return toFailure(error, 'courier.remittance.close');
  }

  revalidatePath('/[locale]/(courier)/me', 'page');
  revalidatePath('/[locale]/(courier)/me/remittances', 'page');
  revalidatePath('/[locale]/(courier)/me/billing', 'page');

  return { ok: true, data: undefined };
}
