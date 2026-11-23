import { defineRouting } from 'next-intl/routing';

/**
 * Ajouter une langue = ajouter un code ici + un fichier `messages/<code>.json`.
 * Rien d'autre : le sens d'écriture est déduit de RTL_LOCALES.
 */
export const routing = defineRouting({
  locales: ['fr', 'ar', 'en'],
  defaultLocale: 'fr',
  localePrefix: 'always',
  /*
   * Le français s'impose à qui n'a rien choisi — C9 bloc 1.
   *
   * Par défaut, next-intl lit `Accept-Language` : un visiteur au navigateur
   * anglophone atterrissait sur `/en`, un arabophone sur `/ar`. La plateforme
   * s'adresse d'abord à des sociétés marocaines qui travaillent en français, et
   * l'en-tête du navigateur dit la langue de l'OS, pas celle du métier.
   *
   * Ce drapeau coupe AUSSI le cookie de next-intl, qui portait le choix
   * explicite de l'utilisateur. On ne veut pas de ça : `proxy.ts` relit donc ce
   * cookie à la main, et lui seul. Le résultat tient en une phrase : l'en-tête
   * ne décide jamais, le choix décide toujours.
   */
  localeDetection: false,
});

/** Cookie de langue de next-intl, relu à la main dans `proxy.ts`. */
export const LOCALE_COOKIE = 'NEXT_LOCALE';

export type AppLocale = (typeof routing.locales)[number];

const RTL_LOCALES = new Set<string>(['ar']);

export function localeDirection(locale: string): 'rtl' | 'ltr' {
  return RTL_LOCALES.has(locale) ? 'rtl' : 'ltr';
}
