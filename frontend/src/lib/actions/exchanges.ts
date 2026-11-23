'use server';

import {
  exchangeCreateSchema,
  type ExchangeDetail,
  parcelExchangeSchema,
} from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import { advanceExchange, createExchange, requestParcelExchange } from '@/lib/api/exchanges';

/**
 * Les gestes d'un échange — chantier « Finance », point 6.
 *
 * Les écritures partent d'une Server Action, les lectures d'un composant
 * serveur (D-018). `src/lib/http.ts` n'est jamais importé par un composant
 * client.
 */

function revalidateExchanges(route: string): void {
  revalidatePath(`/[locale]/(app)/${route}`, 'page');
}

export async function createExchangeAction(
  input: unknown,
): Promise<ReferentialResult<ExchangeDetail>> {
  const parsed = exchangeCreateSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const exchange = await createExchange(parsed.data);
    revalidateExchanges('exchanges');
    // La fiche du colis d'origine porte désormais le lien : elle doit le voir.
    revalidatePath('/[locale]/(app)/parcels/[code]', 'page');
    return { ok: true, data: exchange };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * ÉCHANGER DEPUIS LE COLIS LIVRÉ — chantier « Étiquette orange », point 2.
 *
 * Trois écrans changent d'un coup : la fiche d'origine porte le bloc « Demande
 * de retour/échange », la liste des colis gagne deux lignes, et le dossier
 * existe. On les revalide tous les trois — un seul oublié montrerait un état
 * d'avant le clic à quelqu'un qui vient de le faire.
 */
export async function parcelExchangeAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<ExchangeDetail>> {
  const parsed = parcelExchangeSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const exchange = await requestParcelExchange(code, parsed.data);
    revalidatePath('/[locale]/(app)/parcels/[code]', 'page');
    revalidateExchanges('parcels');
    revalidateExchanges('exchanges');
    return { ok: true, data: exchange };
  } catch (error) {
    return toFailure(error);
  }
}

export async function advanceExchangeAction(
  code: string,
  step: 'pickup' | 'deliver' | 'close' | 'cancel',
  comment: string,
): Promise<ReferentialResult<ExchangeDetail>> {
  try {
    const exchange = await advanceExchange(code, step, { comment });
    revalidateExchanges('exchanges/[code]');
    revalidateExchanges('exchanges');
    return { ok: true, data: exchange };
  } catch (error) {
    return toFailure(error);
  }
}
