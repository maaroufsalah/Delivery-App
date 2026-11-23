'use server';

import { type MerchantCompany, merchantCompanySchema } from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import type { ReferentialResult } from '@/lib/actions/action-result';
import { toFailure } from '@/lib/actions/action-result';
import { apiFetch } from '@/lib/http';

/**
 * « Ma société » — V2 vendeur bloc 9.
 *
 * Le schéma est rejoué ici alors que le formulaire l'a déjà validé : une
 * Server Action est une frontière réseau, et ce qui lui arrive n'a pas
 * forcément traversé le formulaire.
 *
 * **Aucun identifiant ne transite.** La route lit le client dans la portée :
 * c'est ce qui la distingue de `/clients/:id`, qui sert l'administration et
 * qu'un vendeur ne doit jamais atteindre.
 */
export async function updateMerchantCompanyAction(
  input: unknown,
): Promise<ReferentialResult<MerchantCompany>> {
  const parsed = merchantCompanySchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const company = await apiFetch<MerchantCompany>('/merchant/me/company', {
      method: 'PATCH',
      body: parsed.data,
    });

    revalidatePath('/[locale]/profile', 'page');
    return { ok: true, data: company };
  } catch (error) {
    return toFailure(error);
  }
}
