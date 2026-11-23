'use server';

import type { AuditLogDetail, ServerErrorOccurrence } from '@delivery/shared';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import { getAuditEntry, getAuditPage } from '@/lib/api/audit';
import { getServerErrorOccurrences, getTechAuditEntry } from '@/lib/api/platform';

/**
 * LA VUE TECHNIQUE, en lecture — recette SpeedyTrans 4, point 5 (D-134).
 *
 * L'API décide de tout : le journal brut n'est rendu qu'à un administrateur de
 * plateforme ou à une session « se connecter en tant que », la console exige
 * `platform.*`. Ces actions ne font que relayer, après le rendu, les gestes
 * « Vue technique » et « occurrences » (D-018 : `lib/http.ts` n'est jamais
 * importé par un composant client).
 */

/**
 * Les lignes BRUTES d'une ligne du journal lisible : toutes les écritures de
 * la même requête, avant/après complets. Depuis l'espace d'un tenant, pour
 * Nocido en « se connecter en tant que ».
 */
export async function technicalGroupAction(
  groupId: string,
): Promise<ReferentialResult<AuditLogDetail[]>> {
  try {
    // La recherche du journal brut accepte l'identifiant de requête (D-064) ;
    // une ligne sans identifiant de requête forme seule son groupe.
    const page = await getAuditPage({ search: groupId, pageSize: 50 });
    const ids = page.items.length > 0 ? page.items.map((row) => row.id) : [groupId];
    return { ok: true, data: await Promise.all(ids.map((id) => getAuditEntry(id))) };
  } catch (error) {
    return toFailure(error, 'journal.technique');
  }
}

/** Une ligne du journal brut d'une société, depuis la console. */
export async function techAuditEntryAction(
  companyId: string,
  id: string,
): Promise<ReferentialResult<AuditLogDetail>> {
  try {
    return { ok: true, data: await getTechAuditEntry(companyId, id) };
  } catch (error) {
    return toFailure(error, 'console.journal');
  }
}

/** Les occurrences d'une erreur, pile comprise, depuis la console. */
export async function serverErrorOccurrencesAction(
  requestId: string,
): Promise<ReferentialResult<ServerErrorOccurrence[]>> {
  try {
    return { ok: true, data: await getServerErrorOccurrences(requestId) };
  } catch (error) {
    return toFailure(error, 'console.erreurs');
  }
}
