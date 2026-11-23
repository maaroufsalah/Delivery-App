import * as SecureStore from 'expo-secure-store';
import { BRAND_TENANT } from '@/config/brand';
import {
  APP_ENVIRONMENT,
  developmentApiOverride,
  PRODUCTION_API_ORIGIN,
} from '@/config/environment';

/**
 * LE tenant, choisi à la première ouverture — M1, docs/DECISIONS.md D-075.
 *
 * **Un seul binaire pour tous les clients.** L'application demande un
 * sous-domaine — « speedy » — et appelle `https://speedy.delivery.nocido.com`.
 * Publier une application par transporteur aurait voulu dire un compte
 * développeur, une fiche de magasin et un cycle de revue par client ; et
 * changer une couleur aurait demandé une soumission.
 *
 * Le slug vit dans le coffre système avec les jetons, et pour la même raison
 * qu'eux : il dit chez quel transporteur travaille la personne qui tient le
 * téléphone, ce qui n'est pas une information à laisser traîner.
 */

const SLUG_KEY = 'tenant.slug';

/**
 * Le domaine de la plateforme. L'écran Société ne choisit que le SOUS-domaine :
 * l'hôte, lui, n'est pas négociable depuis l'application.
 */
const PLATFORM_DOMAIN = 'delivery.nocido.com';

/**
 * Ce qu'un slug a le droit d'être.
 *
 * Volontairement strict : il entre dans une URL. Tout ce qui n'est pas une
 * lettre, un chiffre ou un tiret est refusé AVANT la requête — un slug bricolé
 * ne doit pas pouvoir désigner un autre hôte que celui qu'on croit appeler.
 */
const SLUG_PATTERN = /^[a-z0-9][a-z0-9-]{1,40}$/;

export function normalizeSlug(raw: string): string {
  return raw.trim().toLowerCase().replace(/\s+/g, '');
}

export function isValidSlug(raw: string): boolean {
  return SLUG_PATTERN.test(normalizeSlug(raw));
}

/**
 * L'URL de base de l'API pour ce slug.
 *
 * **HTTPS en dur.** Pas de paramètre, pas de repli en clair : un jeton de
 * session qui part en HTTP sur le wifi d'une agence est lisible par tout le
 * monde sur ce wifi. Le développement contre une API locale passe par un
 * tunnel, pas par un assouplissement du code livré.
 */
export function apiBaseUrl(slug: string): string {
  return `${originFor(slug)}/api`;
}

/**
 * L'ORIGINE appelée — chantier UAT, D-200.
 *
 * L'application de production (canal `production`) n'a qu'une adresse,
 * `api.speedytrans.ma`, où nginx pose l'hôte du tenant : le slug n'y entre
 * pas. La recette garde le sous-domaine de la plateforme, comme depuis M1.
 * Voir `config/environment.ts` : c'est le canal natif qui tranche.
 */
function originFor(slug: string): string {
  if (APP_ENVIRONMENT === 'production') return PRODUCTION_API_ORIGIN;

  return developmentApiOverride() ?? `https://${normalizeSlug(slug)}.${PLATFORM_DOMAIN}`;
}

/**
 * La page d'INSCRIPTION du tenant, sur le web — L2 bloc 8.
 *
 * L'inscription publique vit côté web (C13) : la reproduire dans l'application
 * aurait fait un second formulaire de création de compte à tenir d'accord avec
 * le premier, pour un geste qu'un livreur fait une fois dans sa vie.
 *
 * Même hôte que l'API, sans le `/api` : c'est la même société, servie par le
 * même sous-domaine. HTTPS en dur, pour la même raison qu'`apiBaseUrl`.
 */
export function registerUrl(slug: string): string {
  // En production, `api.speedytrans.ma/register` est redirigé par nginx vers
  // le web : le nom du web peut changer au go-live sans toucher l'application.
  return `${originFor(slug)}/register`;
}

/**
 * Le tenant courant — C15 bloc 4.
 *
 * La constante de build gagne sur le coffre, et c'est ce qui rend
 * l'application mono-client : plus personne n'a à saisir son « nom court ».
 * Le coffre reste lu quand `BRAND_TENANT` vaut `null`, c'est-à-dire dans la
 * variante multi-client — le chemin d'origine est intact.
 *
 * **La valeur du coffre n'est même pas consultée** quand la marque est posée :
 * un slug écrit par une version antérieure de l'application ne doit pas
 * pouvoir faire appeler un autre transporteur que celui que ce binaire sert.
 */
export async function readSlug(): Promise<string | null> {
  if (BRAND_TENANT) return BRAND_TENANT;

  return SecureStore.getItemAsync(SLUG_KEY);
}

export async function saveSlug(slug: string): Promise<void> {
  await SecureStore.setItemAsync(SLUG_KEY, normalizeSlug(slug));
}

export async function forgetSlug(): Promise<void> {
  await SecureStore.deleteItemAsync(SLUG_KEY);
}
