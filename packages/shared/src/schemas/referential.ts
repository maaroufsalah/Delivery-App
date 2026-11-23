import { z } from 'zod';
import { madAmountSchema } from './parcel';

/**
 * Schémas d'écriture du référentiel, partagés par le formulaire web et l'API.
 *
 * Convention reprise de `createParcelSchema` : un montant se SAISIT en dirhams
 * (`madAmountSchema`) et se LIT en centimes. La conversion est faite une fois,
 * côté service, par `fromCents`/`toCents` — jamais dans un composant.
 */

const nameSchema = z.string().trim().min(2, 'too-short').max(80);

/**
 * Code de ville : 2 à 3 lettres, majuscules.
 *
 * DOMAIN.md §3.2 — il entre dans le code colis (`CAS-RAB-000123`). Le
 * verrouiller après le premier colis n'est pas une coquetterie : le legacy
 * laissait renommer, et les codes déjà imprimés sur les étiquettes ne
 * correspondaient plus à rien.
 */
export const cityCodeSchema = z
  .string()
  .trim()
  .toUpperCase()
  .regex(/^[A-Z]{2,3}$/, 'invalid-city-code');

export const citySchema = z.object({
  name: nameSchema,
  /**
   * LE NOM EN ARABE — chantier « Étiquette orange », point 1.
   *
   * « Ville de destination en grand (français + arabe). » Facultatif : aucune
   * translittération automatique ne serait juste sur les villes marocaines, et
   * l'étiquette se contente du français tant que la ligne est vide.
   */
  nameAr: z.string().trim().max(120, 'too-large').default(''),
  code: cityCodeSchema,
  zoneId: z.string().min(1, 'required'),
  isActive: z.boolean().default(true),
});

export type CityInput = z.input<typeof citySchema>;
export type CityPayload = z.output<typeof citySchema>;

export const zoneSchema = z.object({
  name: nameSchema,
  baseRate: madAmountSchema,
  sameCityRate: madAmountSchema,
  deliveryDays: z.number().int().min(0, 'negative').max(30, 'too-large').default(2),
  isActive: z.boolean().default(true),
  /** Rattachements multiples — remplace le CSV `zones.villes` (dette n°15). */
  cityIds: z.array(z.string().min(1)).default([]),
});

export type ZoneInput = z.input<typeof zoneSchema>;
export type ZonePayload = z.output<typeof zoneSchema>;

export const zoneRateSchema = z.object({
  fromZoneId: z.string().min(1, 'required'),
  toZoneId: z.string().min(1, 'required'),
  rate: madAmountSchema,
  deliveryDays: z.number().int().min(0, 'negative').max(30, 'too-large').default(2),
  isActive: z.boolean().default(true),
});

export type ZoneRateInput = z.input<typeof zoneRateSchema>;
export type ZoneRatePayload = z.output<typeof zoneRateSchema>;

export const districtSchema = z.object({
  cityId: z.string().min(1, 'required'),
  name: nameSchema,
  isActive: z.boolean().default(true),
});

export type DistrictInput = z.input<typeof districtSchema>;
export type DistrictPayload = z.output<typeof districtSchema>;

/**
 * Tarif d'une paire origine → destination.
 *
 * Les trois cibles de livraison sont tarifées séparément (DOMAIN.md §3.1) :
 * livrer au quartier, en agence ou en point relais n'a pas le même coût. En
 * prod 4 804/4 804 colis sont `district`, mais la grille les porte toutes.
 */
export const pricingRuleSchema = z.object({
  originCityId: z.string().min(1, 'required'),
  destinationCityId: z.string().min(1, 'required'),
  rateDistrict: madAmountSchema,
  rateAgency: madAmountSchema,
  rateRelayPoint: madAmountSchema,
  isEnabled: z.boolean().default(true),
});

export type PricingRuleInput = z.input<typeof pricingRuleSchema>;
export type PricingRulePayload = z.output<typeof pricingRuleSchema>;

/**
 * Écriture de la grille d'UNE origine — chantier « Tarifs par origine ».
 *
 * La DESTINATION identifie la ligne ; l'origine vient de l'URL. `rate: null`
 * retire la règle et rend la destination au tarif de zone — une case vidée
 * n'est pas un tarif à zéro. `deliveryDelay` absent : le délai ne bouge pas ;
 * `null` : il est effacé.
 */
export const pricingOriginRowSchema = z.object({
  destinationCityId: z.string().min(1, 'required'),
  rate: madAmountSchema.nullable(),
  deliveryDelay: z.string().trim().max(40, 'too-long').nullable().optional(),
});

export const pricingOriginSaveSchema = z.object({
  rows: z.array(pricingOriginRowSchema).min(1, 'required').max(1000, 'too-many'),
});

export type PricingOriginSaveInput = z.input<typeof pricingOriginSaveSchema>;
export type PricingOriginSavePayload = z.output<typeof pricingOriginSaveSchema>;

export const deliveryTypeSchema = z.object({
  name: nameSchema,
  /** Millièmes entiers : 1000 = ×1. Aucun flottant sur un facteur de prix. */
  multiplierPerMille: z.number().int().min(1, 'too-small').max(10_000, 'too-large'),
  isActive: z.boolean().default(true),
  isDefault: z.boolean().default(false),
});

export type DeliveryTypeInput = z.input<typeof deliveryTypeSchema>;
export type DeliveryTypePayload = z.output<typeof deliveryTypeSchema>;

/** Activation / désactivation : la même route pour toutes les entités. */
export const toggleActiveSchema = z.object({ isActive: z.boolean() });
