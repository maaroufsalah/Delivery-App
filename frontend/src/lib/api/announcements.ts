import type {
  AnnouncementDetail,
  AnnouncementFiltersInput,
  AnnouncementListItem,
  AnnouncementPayload,
  Paginated,
} from '@delivery/shared';
import { apiFetch } from '@/lib/http';

/**
 * Actualités — `GET /announcements`, `GET /announcements/feed`,
 * `POST /announcements`, `PATCH /announcements/:id`.
 *
 * Deux lectures, deux besoins : `getAnnouncements` est la vue de RÉDACTION
 * (brouillons compris, réservée à qui publie) ; `getAnnouncementFeed` est la vue
 * de LECTURE, déjà filtrée par l'API sur la cible du compte.
 */

export async function getAnnouncements(
  query: AnnouncementFiltersInput = {},
): Promise<Paginated<AnnouncementListItem>> {
  return apiFetch<Paginated<AnnouncementListItem>>('/announcements', {
    query: {
      page: query.page as number | undefined,
      pageSize: query.pageSize as number | undefined,
      search: query.search,
      audience: query.audience,
      published: query.published,
    },
  });
}

/** Les actualités publiées qui concernent le compte connecté. */
export async function getAnnouncementFeed(limit = 3): Promise<AnnouncementDetail[]> {
  return apiFetch<AnnouncementDetail[]>('/announcements/feed', { query: { limit } });
}

export async function getAnnouncement(id: string): Promise<AnnouncementDetail> {
  return apiFetch<AnnouncementDetail>(`/announcements/${encodeURIComponent(id)}`);
}

export async function createAnnouncement(input: AnnouncementPayload): Promise<AnnouncementDetail> {
  return apiFetch<AnnouncementDetail>('/announcements', { method: 'POST', body: input });
}

export async function updateAnnouncement(
  id: string,
  input: AnnouncementPayload,
): Promise<AnnouncementDetail> {
  return apiFetch<AnnouncementDetail>(`/announcements/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: input,
  });
}

export async function deleteAnnouncement(id: string): Promise<{ id: string }> {
  return apiFetch<{ id: string }>(`/announcements/${encodeURIComponent(id)}`, {
    method: 'DELETE',
  });
}
