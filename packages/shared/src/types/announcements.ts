import type { AnnouncementAudience } from '../enums';
import type { Id, IsoDateTime } from './common';

/**
 * Actualités — DOMAIN.md §1.20, C7 bloc 3.
 *
 * Le corps est du TEXTE, pas du HTML : un éditeur riche stocké en HTML oblige à
 * assainir à l'affichage, sans quoi c'est une injection offerte à qui a le
 * droit de publier. Le rendu reconnaît un sous-ensemble volontairement pauvre
 * — paragraphes, gras, listes, liens — et le compose en React.
 */
export interface AnnouncementListItem {
  id: Id;
  title: string;
  intro: string | null;
  audience: AnnouncementAudience;
  isPublished: boolean;
  publishedAt: IsoDateTime | null;
  hasImage: boolean;
  imageName: string | null;
  authorName: string;
  createdAt: IsoDateTime;
  updatedAt: IsoDateTime;
}

export interface AnnouncementDetail extends AnnouncementListItem {
  body: string;
}
