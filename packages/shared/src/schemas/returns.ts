import { z } from 'zod';
import { RETURN_REASONS } from '../enums';
import { hasSelection, optionalIds, selectionByCriteria, SELECTION_MAX } from './selection';

/**
 * Schémas d'écriture du menu RETOURS, partagés par les formulaires web et
 * l'API — même convention que le référentiel et les comptes.
 *
 * Ce qui n'est PAS ici : les frais. Ils ne se saisissent pas, ils sont lus dans
 * les réglages métier (D-019) au moment où la ligne est créée, puis gelés. Un
 * champ de saisie les rendrait négociables ligne par ligne, ce qui est
 * exactement ce que le legacy laissait faire.
 */

const optionalText = (max: number) => z.string().trim().max(max).optional().or(z.literal(''));

/* --- Remise du livreur à l'agence ---------------------------------------- */

export const returnHandoverSchema = z.object({
  /**
   * LE LIVREUR ET L'AGENCE — FACULTATIFS depuis le chantier « Bon de retour
   * livreur », point 1.
   *
   * L'agence les envoie : c'est elle qui choisit à QUI elle demande un retour.
   * Le livreur, lui, n'envoie RIEN — les deux viennent de sa fiche, côté
   * serveur.
   *
   * Ce n'est pas du confort. Un écran qui passe son propre identifiant donne
   * l'illusion d'une garantie que le premier appel direct à l'API contourne,
   * et donne surtout à croire qu'on peut en passer un AUTRE. C'est la règle
   * D-082, et c'est la forme qu'ont déjà `POST /merchant/me/pickups` et
   * `/dropoffs` : aucun identifiant dans le corps, la portée le donne.
   */
  courierId: z.string().min(1).optional(),
  agencyId: z.string().min(1).optional(),
  note: optionalText(500),
  /**
   * LA SÉLECTION, À L'OUVERTURE — chantier « Retours, BL Clients Pro »,
   * point 2, étape 3 « Créer le bon de retour ».
   *
   * Facultative, et c'est ce qui évite une régression : l'écran des remises
   * ouvre toujours un bon vide qu'on remplit ensuite à la douchette. L'assistant
   * de l'admin, lui, a déjà coché ses lignes — les lui faire rescanner une à
   * une après les avoir choisies n'aurait aucun sens.
   */
  // Borne relevée de 300 à `SELECTION_MAX` (chantier « Plafond de 200 »).
  parcelIds: z.array(z.string().min(1)).max(SELECTION_MAX, 'too-many').optional(),
  /** « Tous les candidats de la recherche », chargés ou non (`selection.ts`). */
  ...selectionByCriteria,
  /**
   * « Modification autorisée par le livreur » — point 2.
   *
   * Par défaut NON : un bon d'origine admin est une demande, et le livreur
   * l'exécute. La case l'ouvre quand l'agent sait que la pile bougera encore.
   */
  courierCanEdit: z.boolean().default(false),
});

export type ReturnHandoverInput = z.input<typeof returnHandoverSchema>;
export type ReturnHandoverPayload = z.output<typeof returnHandoverSchema>;

/**
 * L'ANNULATION D'UN BON — point 1.
 *
 * Le motif est facultatif mais proposé : un bon qui disparaît sans raison est
 * un bon qu'on rouvrira à l'identique la semaine suivante. Ce qui n'est PAS
 * facultatif, c'est la trace : l'annulation entre au journal avec son auteur.
 */
export const returnHandoverCancelSchema = z.object({
  reason: optionalText(500),
});

export type ReturnHandoverCancelPayload = z.output<typeof returnHandoverCancelSchema>;

/**
 * LA PRISE EN CHARGE PAR LE LIVREUR — point 2, « scan ou manuel, comme au
 * ramassage ».
 *
 * Le corps est vide par défaut : le bon porte déjà ses colis, et le livreur
 * atteste les avoir. `parcelCodes` sert le chemin PAR SCAN : le livreur passe
 * la douchette sur chaque carton, et l'API vérifie que ce sont bien ceux du
 * bon — même contrôle qu'au ramassage (D-094), même refus motivé sinon.
 */
export const returnHandoverPickupSchema = z.object({
  parcelCodes: z.array(z.string().trim().min(1).max(64)).max(300, 'too-many').optional(),
});

export type ReturnHandoverPickupPayload = z.output<typeof returnHandoverPickupSchema>;

/**
 * AJOUT DE COLIS EN LOT à un bon de retour — point 2, la multi-sélection.
 *
 * Le geste unitaire reste, et il n'est pas redondant : c'est celui de la
 * douchette, qui part d'un CODE lu sur un carton. Celui-ci part d'identifiants
 * cochés dans une liste. Même destination, deux entrées, et un corps qui
 * accepterait les deux se lirait comme un `if` déguisé en contrat.
 */
export const returnHandoverParcelsSchema = z
  .object({
    parcelIds: optionalIds,
    ...selectionByCriteria,
    note: optionalText(255),
  })
  .refine((value) => hasSelection({ all: value.all, ids: value.parcelIds }), {
    message: 'required',
    path: ['parcelIds'],
  });

export type ReturnHandoverParcelsPayload = z.output<typeof returnHandoverParcelsSchema>;

/**
 * Ajout d'un colis à une remise, par SCAN.
 *
 * Le code est la seule entrée : c'est une douchette qui le fournit, et elle
 * tape vite. On ne demande donc ni longueur exacte ni format — le code colis
 * fait 15 caractères aujourd'hui (D-008), mais un import legacy en porte de
 * plus courts, et refuser au clavier ce que la base contient bloquerait
 * l'agent devant un colis bien réel.
 */
export const returnScanSchema = z.object({
  code: z
    .string()
    .trim()
    .min(3, 'too-short')
    .max(64)
    .transform((value) => value.toUpperCase()),
  note: optionalText(255),
});

export type ReturnScanPayload = z.output<typeof returnScanSchema>;

/* --- Bon de retour au client --------------------------------------------- */

export const returnSlipSchema = z.object({
  clientId: z.string().min(1, 'required'),
  agencyId: z.string().min(1, 'required'),
  note: optionalText(500),
});

export type ReturnSlipInput = z.input<typeof returnSlipSchema>;
export type ReturnSlipPayload = z.output<typeof returnSlipSchema>;

export const returnSlipItemSchema = z.object({
  parcelId: z.string().min(1, 'required'),
  reason: z.enum(RETURN_REASONS).default('other'),
});

export type ReturnSlipItemInput = z.input<typeof returnSlipItemSchema>;
export type ReturnSlipItemPayload = z.output<typeof returnSlipItemSchema>;

/**
 * Ajout de colis EN LOT à un bon de retour — retouches Anass, point 7.
 *
 * « Multi-sélection, recherche par code / destinataire / téléphone. » Un bon de
 * retour porte la marchandise d'une semaine d'échecs ; l'ajouter colis par
 * colis dans un sélecteur, c'est trente allers-retours et autant d'occasions de
 * perdre la moitié d'un lot sur une coupure réseau.
 *
 * **Un seul motif pour le lot**, et c'est fidèle au geste : on rend une pile
 * de colis refusés, ou une pile d'injoignables. Un motif par ligne reste
 * possible en les ajoutant séparément — le formulaire unitaire ne disparaît
 * pas.
 */
export const returnSlipItemsSchema = z
  .object({
    parcelIds: optionalIds,
    ...selectionByCriteria,
    reason: z.enum(RETURN_REASONS).default('other'),
  })
  .refine((value) => hasSelection({ all: value.all, ids: value.parcelIds }), {
    message: 'required',
    path: ['parcelIds'],
  });

export type ReturnSlipItemsPayload = z.output<typeof returnSlipItemsSchema>;

/**
 * Clôture d'un bon : « remis au client ».
 *
 * Le nom du receveur est OBLIGATOIRE. `DOMAIN.md §4` dette n°4 : le legacy
 * avait un code de retour et un mot de passe — stocké en clair dans
 * `users.password_retour` — dont la vérification était désactivée par un
 * `if (!$userCode && false)`. On ne remplace pas un secret inutile par un autre
 * secret : on demande qui a signé, et on le trace.
 */
export const returnSlipCloseSchema = z.object({
  receivedByName: z.string().trim().min(2, 'too-short').max(120),
  /** Pièce jointe facultative : photo du bon signé, en base64. */
  proof: z
    .object({
      name: z.string().trim().min(1, 'required').max(120),
      mimeType: z.enum(['application/pdf', 'image/jpeg', 'image/png', 'image/webp'], {
        error: 'unsupported-file-type',
      }),
      content: z.string().min(1, 'required'),
    })
    .nullable()
    .default(null),
});

export type ReturnSlipCloseInput = z.input<typeof returnSlipCloseSchema>;
export type ReturnSlipClosePayload = z.output<typeof returnSlipCloseSchema>;
