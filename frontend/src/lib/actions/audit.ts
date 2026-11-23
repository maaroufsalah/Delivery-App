'use server';

import type {
  AuditLogDetail,
  AuditLogListItem,
  JournalEntry,
  JournalEntryDetail,
  Paginated,
} from '@delivery/shared';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import {
  getAuditEntry,
  getEntityHistory,
  getEntityJournal,
  getJournalEntry,
} from '@/lib/api/audit';

/**
 * Le journal, en lecture — C11 bloc 5, D-064.
 *
 * **Aucune action d'écriture, et il n'y en aura pas.** Le journal est
 * append-only ; une Server Action de suppression serait un endpoint à écrire en
 * face, et un journal qu'on peut effacer ne prouve rien.
 *
 * Deux lectures seulement, et elles passent par une action plutôt que par un
 * composant serveur parce qu'elles arrivent APRÈS le rendu : ouvrir le détail
 * d'une entrée et charger l'onglet « Historique » sont deux gestes de
 * l'utilisateur, pas deux chargements de page. `src/lib/http.ts` n'est jamais
 * importé par un composant client (D-018).
 */

export async function auditEntryAction(id: string): Promise<ReferentialResult<AuditLogDetail>> {
  try {
    return { ok: true, data: await getAuditEntry(id) };
  } catch (error) {
    return toFailure(error);
  }
}

export async function entityHistoryAction(
  entity: string,
  entityId: string,
): Promise<ReferentialResult<Paginated<AuditLogListItem>>> {
  try {
    return { ok: true, data: await getEntityHistory(entity, entityId) };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Le journal LISIBLE — recette SpeedyTrans 4, point 4 (D-133) --------- */

export async function journalEntryAction(
  id: string,
): Promise<ReferentialResult<JournalEntryDetail>> {
  try {
    return { ok: true, data: await getJournalEntry(id) };
  } catch (error) {
    return toFailure(error);
  }
}

export async function entityJournalAction(
  entity: string,
  entityId: string,
): Promise<ReferentialResult<Paginated<JournalEntry>>> {
  try {
    return { ok: true, data: await getEntityJournal(entity, entityId) };
  } catch (error) {
    return toFailure(error);
  }
}
