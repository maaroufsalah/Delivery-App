import { z } from 'zod';
import { PASSWORD_MIN_LENGTH } from '../password';
import { optionalUsernameSchema } from './accounts';
import { phoneSchema } from './parcel';

/**
 * Inscription PUBLIQUE sur l'espace d'un tenant — C13 bloc 1, D-086.
 *
 * Ce que ce schéma ne contient PAS est aussi important que ce qu'il contient :
 *
 * - **aucun identifiant de société.** Elle vient de l'hôte, résolue côté
 *   serveur. Un champ société laisserait choisir chez qui l'on s'inscrit, et
 *   c'est exactement ce que le multi-tenant refuse (C8, D-049) ;
 * - **aucun rôle.** Il se déduit du type de demande, jamais de la requête ;
 * - **aucune confirmation de mot de passe.** L'œil du champ montre ce qui a
 *   été tapé, ce qui vaut mieux qu'un second champ recopié de travers ;
 * - **aucune agence.** Elle sera choisie par l'administrateur qui valide
 *   (bloc 2), et elle n'a de sens que pour lui.
 */

/*
 * LA borne commune, et le message commun avec elle.
 *
 * L'inscription a porté un temps sa propre constante et son propre code
 * `password-too-short-8`, parce que le message partagé annonçait dix
 * caractères pour un champ qui en acceptait huit. Les deux bornes n'en font
 * plus qu'une (`PASSWORD_MIN_LENGTH`) et le message reçoit le nombre en
 * paramètre : il n'y a plus de règle à distinguer, donc plus de message à
 * dédoubler.
 */
const passwordSchema = z.string().min(PASSWORD_MIN_LENGTH, 'password-too-short').max(200);

const nameSchema = z.string().trim().min(2, 'too-short').max(80);

/** Commun aux deux formulaires : l'identité, le contact, le secret. */
const baseRegistrationSchema = z.object({
  firstName: nameSchema,
  lastName: nameSchema,
  // Lot D, D1 (D-188) : chiffres et « + », tel que saisi, comme partout.
  phone: phoneSchema,
  cityId: z.string().min(1, 'required'),
  email: z.email({ error: 'invalid-email' }).transform((value) => value.trim().toLowerCase()),
  password: passwordSchema,
});

export const merchantRegistrationSchema = baseRegistrationSchema.extend({
  kind: z.literal('MERCHANT'),
  /** Le nom de la BOUTIQUE, qui deviendra `Client.name`. */
  shopName: z.string().trim().min(2, 'too-short').max(120),
});

/**
 * LE LIVREUR PEUT S'INSCRIRE SANS E-MAIL — recette SpeedyTrans 3, point 5.
 *
 * Il choisit un nom d'utilisateur, et c'est avec lui qu'il se connectera une
 * fois la demande validée. L'un des deux, au moins ; les deux s'il le veut.
 */
export const courierRegistrationSchema = baseRegistrationSchema
  .omit({ email: true })
  .extend({
    kind: z.literal('COURIER'),
    // `nullish()` : la Server Action revalide la sortie du formulaire (`null`).
    email: z
      .email({ error: 'invalid-email' })
      .transform((value) => value.trim().toLowerCase())
      .nullish()
      .or(z.literal(''))
      .transform((value) => value || null),
    username: optionalUsernameSchema,
  })
  .superRefine((value, ctx) => {
    if (value.email || value.username) return;
    ctx.addIssue({ code: 'custom', path: ['username'], message: 'email-or-username' });
  });

/**
 * Union DISCRIMINÉE plutôt qu'un objet aux champs facultatifs : le nom de
 * boutique est obligatoire pour un e-commerçant et n'a aucun sens pour un
 * livreur. Un schéma unique avec `shopName?` laisserait passer l'un sans
 * l'autre, et l'API devrait le revérifier à la main.
 */
export const registrationSchema = z.discriminatedUnion('kind', [
  merchantRegistrationSchema,
  courierRegistrationSchema,
]);

export type MerchantRegistrationInput = z.input<typeof merchantRegistrationSchema>;
export type CourierRegistrationInput = z.input<typeof courierRegistrationSchema>;
export type RegistrationInput = z.input<typeof registrationSchema>;
export type RegistrationPayload = z.output<typeof registrationSchema>;

export const REGISTRATION_KINDS = ['MERCHANT', 'COURIER'] as const;
export type RegistrationKind = (typeof REGISTRATION_KINDS)[number];

/**
 * Ce que l'API rend après une inscription — et ce qu'elle ne rend pas.
 *
 * Pas de session, pas de jeton, pas d'identifiant de fiche : le compte est en
 * attente, et lui ouvrir quoi que ce soit serait contredire la validation.
 */
export interface RegistrationResult {
  ok: true;
  kind: RegistrationKind;
}

/**
 * Une ville, telle que l'écran d'inscription a besoin de la proposer.
 *
 * Servie SANS session : le formulaire vit sur la page publique. Elle ne porte
 * rien d'autre que ce qu'un panneau de signalisation affiche déjà.
 */
export interface PublicCity {
  id: string;
  name: string;
}
