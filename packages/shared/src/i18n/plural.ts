/**
 * LES PLURIELS DU TÉLÉPHONE — Recette 5, point 8.
 *
 * « Libellés au pluriel correct en fr/en/ar. » Le web a next-intl, qui lit la
 * syntaxe ICU ; le téléphone n'avait qu'un remplacement de `{nom}`, et Hermes
 * ne garantit pas `Intl.PluralRules`. On écrit donc ici les catégories CLDR des
 * trois langues servies, et un lecteur du sous-ensemble ICU dont les messages
 * ont besoin :
 *
 *     '{count, plural, one {# colis validé} other {# colis validés}}'
 *
 * La même chaîne vaut pour next-intl : un message s'écrit d'une seule façon,
 * sur le web comme sur le téléphone.
 */
export type PluralCategory = 'zero' | 'one' | 'two' | 'few' | 'many' | 'other';

/** Les règles CLDR des entiers, pour les trois langues servies. */
export function pluralCategory(locale: string, count: number): PluralCategory {
  const n = Math.abs(Math.trunc(count));
  const language = locale.slice(0, 2);

  if (language === 'ar') {
    if (n === 0) return 'zero';
    if (n === 1) return 'one';
    if (n === 2) return 'two';
    const mod = n % 100;
    if (mod >= 3 && mod <= 10) return 'few';
    if (mod >= 11 && mod <= 99) return 'many';
    return 'other';
  }

  // Le français met 0 au singulier (« 0 colis validé ») ; l'anglais, non.
  if (language === 'fr') return n < 2 ? 'one' : 'other';
  return n === 1 ? 'one' : 'other';
}

/** L'accolade fermante qui répond à celle ouverte en `start`. */
function closing(text: string, start: number): number {
  let depth = 0;
  for (let i = start; i < text.length; i += 1) {
    if (text[i] === '{') depth += 1;
    else if (text[i] === '}') {
      depth -= 1;
      if (depth === 0) return i;
    }
  }
  return -1;
}

/** Les branches `one {…} other {…}` d'un bloc pluriel. */
function branches(body: string): Map<string, string> {
  const found = new Map<string, string>();
  let i = 0;
  while (i < body.length) {
    const open = body.indexOf('{', i);
    if (open === -1) break;
    const selector = body.slice(i, open).trim();
    const end = closing(body, open);
    if (end === -1) break;
    found.set(selector, body.slice(open + 1, end));
    i = end + 1;
  }
  return found;
}

/**
 * Formate un message : `{nom}` remplacé, `{nom, plural, …}` résolu selon la
 * langue (`=0`, `=1`… d'abord, puis la catégorie, puis `other`), `#` remplacé
 * par le nombre. Un message sans bloc pluriel se lit comme avant.
 */
export function formatMessage(
  template: string,
  values: Record<string, string | number> | undefined,
  locale: string,
): string {
  if (!values) return template;

  let out = '';
  let i = 0;
  while (i < template.length) {
    const open = template.indexOf('{', i);
    if (open === -1) {
      out += template.slice(i);
      break;
    }
    out += template.slice(i, open);
    const end = closing(template, open);
    if (end === -1) {
      out += template.slice(open);
      break;
    }

    const inner = template.slice(open + 1, end);
    const match = /^\s*(\w+)\s*,\s*plural\s*,([\s\S]*)$/.exec(inner);

    if (match) {
      const name = match[1] ?? '';
      const count = Number(values[name] ?? 0);
      const options = branches(match[2] ?? '');
      const chosen =
        options.get(`=${count}`) ??
        options.get(pluralCategory(locale, count)) ??
        options.get('other') ??
        '';
      out += formatMessage(chosen.replaceAll('#', String(count)), values, locale);
    } else {
      const name = inner.trim();
      out += name in values ? String(values[name]) : `{${inner}}`;
    }
    i = end + 1;
  }
  return out;
}
