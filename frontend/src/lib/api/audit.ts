import type {
  AuditLogDetail,
  AuditLogListItem,
  JournalEntry,
  JournalEntryDetail,
  JournalQuery,
  Paginated,
} from '@delivery/shared';
import { apiFetch } from '@/lib/http';

/**
 * Le journal des modifications — C11 bloc 5, D-064.
 *
 * **Aucune fonction d'écriture, et il n'y en aura pas** : le journal est
 * append-only, et il est écrit sous les services par une extension Prisma.
 * Une fonction de suppression ici serait un endpoint à écrire en face.
 */

type Query = Record<string, string | number | undefined>;

function toSearch(query: object): Record<string, string | number> {
  const search: Record<string, string | number> = {};

  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue;
    search[key] = value as string | number;
  }

  return search;
}

export async function getAuditPage(
  query: Query & { entity?: string; action?: string; userId?: string },
): Promise<Paginated<AuditLogListItem>> {
  return apiFetch<Paginated<AuditLogListItem>>('/audit', { query: toSearch(query) });
}

/** Une entrée, avec son avant et son après entiers — le détail côte à côte. */
export async function getAuditEntry(id: string): Promise<AuditLogDetail> {
  return apiFetch<AuditLogDetail>(`/audit/entry/${encodeURIComponent(id)}`);
}

/**
 * L'historique d'UNE ligne — l'onglet « Historique » des fiches.
 *
 * Pas de permission côté API : qui peut ouvrir la fiche peut lire son histoire,
 * et la portée serveur borne ce qu'il voit.
 */
export async function getEntityHistory(
  entity: string,
  entityId: string,
  query: Query = {},
): Promise<Paginated<AuditLogListItem>> {
  return apiFetch<Paginated<AuditLogListItem>>(
    `/audit/${encodeURIComponent(entity)}/${encodeURIComponent(entityId)}`,
    { query: toSearch({ pageSize: 50, ...query }) },
  );
}

/* -------------------------------------------------------------------------- */
/* Le journal LISIBLE — recette SpeedyTrans 4, point 4 (D-133)                */
/* -------------------------------------------------------------------------- */

/** Une ligne métier par geste : `GET /audit/journal`. */
export async function getJournal(query: JournalQuery = {}): Promise<Paginated<JournalEntry>> {
  return apiFetch<Paginated<JournalEntry>>('/audit/journal', { query: toSearch(query) });
}

/** Une ligne avec son détail (champs métier, motif, adresse) : `GET /audit/journal/entry/:id`. */
export async function getJournalEntry(id: string): Promise<JournalEntryDetail> {
  return apiFetch<JournalEntryDetail>(`/audit/journal/entry/${encodeURIComponent(id)}`);
}

/** L'onglet « Historique » d'une fiche, en langage métier. */
export async function getEntityJournal(
  entity: string,
  entityId: string,
): Promise<Paginated<JournalEntry>> {
  return apiFetch<Paginated<JournalEntry>>(
    `/audit/journal/entity/${encodeURIComponent(entity)}/${encodeURIComponent(entityId)}`,
    { query: { pageSize: 50 } },
  );
}
