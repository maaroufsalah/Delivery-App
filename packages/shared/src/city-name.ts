/**
 * Noms de ville SAISIS À LA MAIN → nom usuel — chantier « Purge de speedy »,
 * repris par « Tarifs par origine ».
 *
 * La grille du transporteur écrit « Fes », « MOUHAMMEDIA », « KENITRA VILLE » ;
 * la ville imprimée sur l'étiquette, elle, doit être juste. Deux usages, UNE
 * règle : `scripts/import-cities.ts` crée les villes avec ces noms, et l'import
 * des tarifs par origine RETROUVE ces villes à partir des mêmes cellules. Deux
 * normalisations divergeraient au premier accent.
 */

/** Un segment en écriture arabe : « Goulmima VILLE -كلميمة ». */
const ARABIC = /[\u0600-\u06FF][\u0600-\u06FF\s]*/u;

/** Marques de sens et espaces de largeur nulle, collés par un copier-coller. */
const INVISIBLE = /[\u200B-\u200F\u202A-\u202E]/g;

/** Les particules restent en minuscules : « Kelâa des Sraghna ». */
const PARTICLES = new Set(['de', 'des', 'du']);

/**
 * Orthographe USUELLE, appliquée mot pour mot après la casse Titre. La grille
 * est saisie à la main, sans accents ni constance (« Fes », « MOUHAMMEDIA »,
 * « Settate ») ; la ville imprimée sur l'étiquette, elle, doit être juste.
 */
const SPELLING: Array<[string, string]> = [
  ['Fes', 'Fès'],
  ['Meknes', 'Meknès'],
  ['Sale', 'Salé'],
  ['Temara', 'Témara'],
  ['Tetouan', 'Tétouan'],
  ['Tetoun', 'Tétouan'],
  ['Kenitra', 'Kénitra'],
  ['Mediouna', 'Médiouna'],
  ['Mouhammedia', 'Mohammedia'],
  ['Beni[- ]Mellal', 'Béni Mellal'],
  ['Settate', 'Settat'],
  ['Ouarzazat', 'Ouarzazate'],
  ['Ifran', 'Ifrane'],
  ['Laayoune', 'Laâyoune'],
  ['Ouzzane', 'Ouazzane'],
  ['Houcima', 'Al Hoceima'],
  ['Tantan', 'Tan-Tan'],
  ['Tan Tan', 'Tan-Tan'],
  ['Mrirte', 'Mrirt'],
  ['Moly Drisse Zarhoune', 'Moulay Idriss Zerhoun'],
  ['Idi Jaber', 'Sidi Jaber'],
  ['Sid L Mokhtar', 'Sidi El Mokhtar'],
  ['M Diq', 'M’diq'],
  ['M Gouna', 'M’Gouna'],
  ['Imouzar Kandre', 'Imouzzer Kandar'],
  ['Bouknadl', 'Bouknadel'],
  ['Oualmes', 'Oulmès'],
  ['Elhajeb', 'El Hajeb'],
  ['Sidi Aadi', 'Sidi Addi'],
  ['Riche', 'Rich'],
];

function titleCase(value: string): string {
  return value
    .toLowerCase()
    .split(' ')
    .map((word, i) =>
      i > 0 && PARTICLES.has(word)
        ? word
        : word.replace(
            /(^|[-'’])(\p{L})/gu,
            (_, sep: string, ch: string) => sep + ch.toUpperCase(),
          ),
    )
    .join(' ');
}

/** Clé de rapprochement : sans accent, sans casse, sans ponctuation. */
export function cityKey(name: string): string {
  return name
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .toLowerCase()
    .replace(/[^a-z0-9]/g, '');
}

/** Nom usuel d'une cellule, et le nom arabe qu'elle porte éventuellement. */
export function normalizeCityName(raw: string): { name: string; nameAr: string | null } {
  let value = raw.normalize('NFC').replace(INVISIBLE, '').trim();

  const arabic = value.match(ARABIC)?.[0].trim() ?? null;
  value = value.replace(/[\s-]*[\u0600-\u06FF][\u0600-\u06FF\s]*/u, '');

  value = value
    .replace(/[\s-]*\bville\b[\s-]*/gi, ' ') // « KENITRA VILLE », « Ourika-VILLE »
    .replace(/-\d+\s*$/, '') // « ERRACHIDIA-02 »
    .replace(/\s*-\s*/g, '-')
    .replace(/\s+/g, ' ')
    .trim();

  let name = titleCase(value);
  name = name.replace(/-Jadida(?!\p{L})/gu, '-El Jadida'); // « Lhawzia-JADIDA »
  for (const [from, to] of SPELLING) {
    name = name.replace(new RegExp(`(?<!\\p{L})${from}(?!\\p{L})`, 'gu'), to);
  }

  return { name, nameAr: arabic };
}
