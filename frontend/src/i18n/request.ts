import { hasLocale } from 'next-intl';
import { getRequestConfig } from 'next-intl/server';
import { notFound } from 'next/navigation';
import * as rootParams from 'next/root-params';
import { companyShortName, DEFAULT_COMPANY_SETTINGS, withShortName } from '@delivery/shared';
import { getCompanySettings } from '@/lib/api/settings';
import { routing } from './routing';

/**
 * La locale est lue depuis le segment racine `[locale]` via `next/root-params`
 * (Next 16), et non depuis un paramètre propagé de layout en layout.
 *
 * SAUF dans une Server Action : `next/root-params` y lève une erreur (« can
 * only be called in the context of a route »). L'action passe alors la locale
 * à `getTranslations({ locale, namespace })`, que next-intl transmet ici dans
 * `locale` : elle prime sur le segment (incident du 21/11/2026, « Livrer la
 * sélection » en page d'erreur).
 *
 * La devise et le fuseau viennent de la configuration société : une
 * installation au Maroc affiche des MAD en Africa/Casablanca, une installation
 * en France des EUR en Europe/Paris, sans toucher au code.
 */
export default getRequestConfig(async ({ locale }) => {
  const candidate = locale ?? (await rootParams.locale());

  if (!hasLocale(routing.locales, candidate)) {
    notFound();
  }

  const settings = await getCompanySettings();
  const catalogue = (await import(`../../messages/${candidate}.json`)).default as Catalogue;

  return {
    locale: candidate,
    messages: withCompanyShortName(candidate, catalogue, companyShortName(settings)),
    formats: buildFormats(settings.currency),
    timeZone: settings.timeZone,
  };
});

type Catalogue = { [key: string]: string | Catalogue };

/**
 * LE NOM COURT DANS LES LIBELLÉS — lot D, D2 (D-189).
 *
 * « Reçu par %shortName% », « Remis à %shortName% »… Le marqueur n'est pas un
 * argument ICU : chaque appel aurait dû le passer, y compris les libellés lus
 * par clé calculée. Il est remplacé ICI, une fois par langue et par nom (le
 * catalogue importé est partagé, il n'est jamais modifié) : serveur et client
 * reçoivent le même texte, puisque le client tient ses messages de ce
 * chargement.
 */
const substituted = new Map<string, Catalogue>();

function withCompanyShortName(locale: string, catalogue: Catalogue, shortName: string): Catalogue {
  const key = `${locale}\u0000${shortName}`;
  const cached = substituted.get(key);
  if (cached) return cached;

  const walk = (node: Catalogue): Catalogue =>
    Object.fromEntries(
      Object.entries(node).map(([entry, value]) => [
        entry,
        typeof value === 'string' ? withShortName(value, shortName) : walk(value),
      ]),
    );

  const result = walk(catalogue);
  substituted.set(key, result);
  return result;
}

/** Formats partagés — typés dans global.d.ts via AppConfig['Formats']. */
export function buildFormats(currency: string) {
  return {
    dateTime: {
      /** Une date MÉTIER sans heure : échéance, date de chèque, borne de période. */
      short: { day: '2-digit', month: '2-digit', year: 'numeric' },
      /**
       * Un INSTANT, à la seconde : « 12/11/2026 14:05:09 », dans le fuseau de
       * la société (`timeZone` plus haut). C'est le format de toute date
       * horodatée d'une liste ou d'une fiche (créé le, confirmé le, clôturé
       * le…). Une date métier sans heure garde `short` : lui inventer
       * « 00:00:00 » serait faux (chantier « remarques de Badr », point c).
       */
      stamp: {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false,
      },
      long: { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' },
      time: { hour: '2-digit', minute: '2-digit' },
      dayMonth: { day: '2-digit', month: 'short' },
      /** Une date SANS l'heure — sous-titre de période, chantier « Filtre de période ». */
      date: { day: '2-digit', month: 'short', year: 'numeric' },
      /** Mois et année, pour l'axe du graphique agrégé au mois. */
      monthYear: { month: 'short', year: 'numeric' },
    },
    number: {
      currency: { style: 'currency', currency, maximumFractionDigits: 2 },
      integer: { maximumFractionDigits: 0 },
      percent: { style: 'percent', maximumFractionDigits: 1 },
    },
  } as const;
}

/** Instance de référence, uniquement là pour typer les clés de format. */
export const formats = buildFormats(DEFAULT_COMPANY_SETTINGS.currency);
