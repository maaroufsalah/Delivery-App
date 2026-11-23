import { z } from 'zod';
import { ANNOUNCEMENT_AUDIENCES } from '../enums';

/**
 * Actualités — C7 bloc 3.
 *
 * L'image voyage en base64 dans le corps JSON, comme les pièces jointes des
 * réclamations et le classeur d'import (D-022) : une Server Action ne poste pas
 * de `multipart`.
 */
export const announcementSchema = z.object({
  title: z.string().trim().min(3, 'too-short').max(160),
  intro: z.string().trim().max(300).optional().or(z.literal('')),
  /**
   * Corps « riche simple » : paragraphes séparés par une ligne vide, `**gras**`,
   * listes en `- `, liens en `[texte](url)`. Rien d'autre n'est reconnu, et
   * rien d'autre n'est rendu — le reste s'affiche littéralement.
   */
  body: z.string().trim().min(10, 'too-short').max(20_000),
  audience: z.enum(ANNOUNCEMENT_AUDIENCES).default('all'),
  isPublished: z.boolean().default(false),
  /** Date affichée et date de tri. Vide : le service pose l'instant de publication. */
  publishedAt: z.string().optional().or(z.literal('')),
  /** Image en base64. `null` retire celle qui est en place ; absent la conserve. */
  image: z
    .object({
      filename: z.string().trim().min(1, 'required').max(200),
      mimeType: z.enum(['image/jpeg', 'image/png', 'image/webp'], 'unsupported-type'),
      content: z.string().min(1, 'required').max(6_000_000, 'file-too-large'),
    })
    .nullable()
    .optional(),
});

export type AnnouncementInput = z.input<typeof announcementSchema>;
export type AnnouncementPayload = z.output<typeof announcementSchema>;

export const announcementFiltersSchema = z.object({
  search: z.string().trim().optional(),
  audience: z.enum(ANNOUNCEMENT_AUDIENCES).optional(),
  /** `true` : seulement les publiées. `false` : seulement les brouillons. */
  published: z.enum(['true', 'false']).optional(),
  page: z.coerce.number().int().min(1).default(1),
  pageSize: z.coerce.number().int().min(5).max(200).default(20),
});

export type AnnouncementFiltersInput = z.input<typeof announcementFiltersSchema>;
export type AnnouncementFiltersPayload = z.output<typeof announcementFiltersSchema>;
