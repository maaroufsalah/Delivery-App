/**
 * LE NUMÉRO NETTOYÉ AVANT D'ÊTRE VALIDÉ — lot L, L2 (D-220, amende D-172).
 *
 * Le vendeur colle « 06 12 34 56 78 », « 06-12-34-56-78 », « (06) 12.34.56.78 »
 * depuis sa messagerie ou son Excel : D-172 refusait tout cela, et l'import
 * rejetait des lignes entières pour un espace. On retire désormais, partout et
 * avant la validation, les séparateurs qu'un humain met dans un numéro :
 * espaces (insécables compris), `-`, `_`, `.`, `(`, `)`, apostrophes. Un `+`
 * n'est gardé qu'en tête ; `00` en tête devient `+`.
 *
 * Ce n'est PAS une validation : une lettre reste, et `phoneSchema` la refuse.
 * Aucune longueur, aucun format marocain (D-172 tient toujours).
 */
// `\s` couvre aussi les espaces insécables (U+00A0, U+202F…) qu'Excel et WhatsApp collent.
const PHONE_SEPARATORS = /[\s\-_.()'’‘`]/g;

export function normalizePhone(phone: string): string {
  const compact = phone.replace(PHONE_SEPARATORS, '');
  const leadingPlus = compact.startsWith('+');
  const body = compact.replace(/\+/g, '');

  if (leadingPlus) return `+${body}`;
  if (body.startsWith('00')) return `+${body.slice(2)}`;
  return body;
}

/**
 * JUSQU'À TROIS TÉLÉPHONES DANS LE CHAMP DU DESTINATAIRE — D-244 (Badr,
 * 12/11/2026). Pas de nouveau champ : le vendeur écrit « 0612345678 /
 * 0712345678 » là où il écrivait un numéro, et la valeur est rangée
 * normalisée, `num1 / num2 / num3`.
 *
 * **Ce qui sépare.**
 *  - `/`, `,` et `;` séparent TOUJOURS : aucun numéro n'en contenait (ils
 *    étaient refusés), il n'y a donc aucun usage à casser.
 *  - un ESPACE ou un POINT ne sépare que deux numéros COMPLETS, au moins
 *    `PHONE_COMPLETE_DIGITS` chiffres de chaque côté. Sinon il est retiré comme
 *    aujourd'hui : « 06 12 34 56 78 » et « 06.12.34.56.78 » restent UN numéro
 *    (D-220).
 *
 * Chaque numéro passe ensuite par `normalizePhone` : la règle d'un numéro ne
 * change pas, et une lettre est toujours refusée par le schéma.
 */
export const RECIPIENT_PHONES_MAX = 3;
export const PHONE_COMPLETE_DIGITS = 9;
export const RECIPIENT_PHONES_JOIN = ' / ';

const digitCount = (text: string): number => text.replace(/\D/g, '').length;

/**
 * Les numéros d'une saisie, chacun nettoyé, doublons et vides retirés. Ne
 * valide rien : ni le nombre, ni les lettres (voir `recipientPhonesSchema`).
 */
export function splitPhones(input: string): string[] {
  const phones: string[] = [];

  for (const segment of input.split(/[/,;]/)) {
    // Les morceaux que sépare un espace ou un point, dans l'ordre.
    const pieces = segment.split(/[\s.]+/).filter((piece) => piece.length > 0);
    let current = '';

    pieces.forEach((piece, index) => {
      const rest = pieces.slice(index).join('');
      const boundary =
        current.length > 0 &&
        digitCount(current) >= PHONE_COMPLETE_DIGITS &&
        digitCount(rest) >= PHONE_COMPLETE_DIGITS;

      if (boundary) {
        phones.push(current);
        current = piece;
      } else {
        current += piece;
      }
    });

    if (current.length > 0) phones.push(current);
  }

  const cleaned = phones.map(normalizePhone).filter((phone) => phone.length > 0);

  return cleaned.filter((phone, index) => cleaned.indexOf(phone) === index);
}

/** La valeur RANGÉE : `num1 / num2 / num3`. Un seul numéro reste tel quel. */
export function normalizeRecipientPhones(input: string): string {
  return splitPhones(input).join(RECIPIENT_PHONES_JOIN);
}

/**
 * Les numéros d'une valeur déjà rangée (ou d'une ancienne valeur à un seul
 * numéro) : ce que lisent les boutons « Appeler », un par numéro.
 */
export function phonesOf(stored: string | null | undefined): string[] {
  return stored ? splitPhones(stored) : [];
}

/**
 * LE NUMÉRO WHATSAPP — L2 bloc 3.
 *
 * `wa.me` attend un numéro international **sans `+` ni séparateur**. Un numéro
 * marocain saisi en local commence par `0` : on le remplace par l'indicatif.
 *
 * **Pourquoi ici et pas sur le téléphone.** Il y en avait deux copies dans
 * `apps/mobile`, et le web en a besoin à son tour (bloc 9 : le livreur doit
 * pouvoir faire sur le web tout ce qu'il fait sur l'application). Trois copies
 * d'une règle de formatage, c'est trois occasions de la corriger deux fois.
 *
 * **Et l'une des deux copies était fausse** : sa classe de caractères s'écrivait
 * `[^d+]` au lieu de `[^\d+]`. Sans la barre oblique inverse, `d` est la LETTRE
 * d — la fonction retirait donc tous les chiffres, rendait une chaîne vide, et
 * le bouton WhatsApp de la fiche colis ouvrait `wa.me/` sans numéro. Le défaut
 * ne se voyait pas en lisant le code ; il se voit en lisant un test.
 */
export function waNumber(phone: string): string {
  const digits = phone.replace(/[^\d+]/g, '');

  if (digits.startsWith('+')) return digits.slice(1);
  /*
   * Le `0` de tête est le préfixe NATIONAL, pas un chiffre du numéro : le
   * garder donnerait `2120612…`, que WhatsApp refuse. L'indicatif marocain est
   * le seul servi — la plateforme ne livre qu'au Maroc (DOMAIN.md §1.4), et un
   * indicatif paramétrable sans pays à servir serait un réglage vide.
   */
  if (digits.startsWith('0')) return `212${digits.slice(1)}`;

  return digits;
}
