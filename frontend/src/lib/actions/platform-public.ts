'use server';

import { trialRequestSchema } from '@delivery/shared';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import { publicFetch } from '@/lib/http';

/**
 * La demande d'essai, déposée depuis le domaine nu — C8 bloc 5.
 *
 * Séparée de `actions/platform.ts` à dessein : celui-là sert la console et
 * n'appelle que des routes exigeant `platform.*`. Celle-ci est la SEULE action
 * publique du domaine plateforme, et les mélanger inviterait à réutiliser un
 * import sans voir qu'on ouvre une route d'administration à un visiteur.
 *
 * `publicFetch` et non `apiFetch` : il n'y a pas de session, et transférer des
 * cookies ici ferait dépendre une demande anonyme de l'état de connexion.
 */
export async function requestTrialAction(input: unknown): Promise<ReferentialResult> {
  const parsed = trialRequestSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await publicFetch('/platform/trial-requests', { method: 'POST', body: parsed.data });
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}
