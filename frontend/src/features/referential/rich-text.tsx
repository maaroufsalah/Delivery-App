import { Fragment } from 'react';

/**
 * Rendu du corps « riche simple » d'une actualité — C7 bloc 3.
 *
 * **Pourquoi pas de HTML stocké.** Un éditeur riche qui enregistre du HTML
 * oblige à l'assainir à chaque affichage : oublier une fois, c'est offrir une
 * injection à qui a le droit de publier. Ici le corps est du TEXTE, et ce
 * module le traduit en composants React — aucun `dangerouslySetInnerHTML`,
 * donc aucune balise ne peut sortir de ce que le rendu connaît.
 *
 * **Pourquoi pas de dépendance.** Une bibliothèque markdown pèse plus lourd que
 * le besoin, et la mission interdit d'en ajouter. Le sous-ensemble reconnu est
 * volontairement pauvre :
 *
 *   - paragraphes séparés par une ligne vide ;
 *   - listes à puces, lignes commençant par `- ` ;
 *   - `**gras**` ;
 *   - liens `[texte](https://…)`.
 *
 * Tout le reste s'affiche littéralement — c'est la bonne dégradation : un
 * rédacteur qui tape du markdown inconnu voit ses caractères, il ne voit pas
 * disparaître son texte.
 */

/** Seuls `http(s)` et `mailto` : `javascript:` dans un lien est un XSS. */
function safeHref(url: string): string | null {
  const trimmed = url.trim();
  return /^(https?:\/\/|mailto:)/i.test(trimmed) ? trimmed : null;
}

/** `**gras**` et `[texte](url)`, en une passe. */
function renderInline(text: string, keyPrefix: string): React.ReactNode[] {
  const nodes: React.ReactNode[] = [];
  const pattern = /\*\*(.+?)\*\*|\[([^\]]+)\]\(([^)\s]+)\)/g;

  let cursor = 0;
  let match: RegExpExecArray | null;
  let index = 0;

  while ((match = pattern.exec(text)) !== null) {
    if (match.index > cursor) nodes.push(text.slice(cursor, match.index));

    if (match[1] !== undefined) {
      nodes.push(<strong key={`${keyPrefix}-b${index}`}>{match[1]}</strong>);
    } else if (match[2] !== undefined && match[3] !== undefined) {
      const href = safeHref(match[3]);
      nodes.push(
        href ? (
          <a
            key={`${keyPrefix}-a${index}`}
            href={href}
            target="_blank"
            rel="noopener noreferrer"
            className="text-primary underline underline-offset-2"
          >
            {match[2]}
          </a>
        ) : (
          // Protocole refusé : on rend le texte, pas le lien. L'auteur voit
          // que quelque chose n'a pas pris, et rien d'exécutable n'est posé.
          <Fragment key={`${keyPrefix}-a${index}`}>{match[2]}</Fragment>
        ),
      );
    }

    cursor = match.index + match[0].length;
    index += 1;
  }

  if (cursor < text.length) nodes.push(text.slice(cursor));
  return nodes;
}

export function RichText({ body, className }: { body: string; className?: string }) {
  const blocks = body.split(/\n{2,}/);

  return (
    <div className={className}>
      {blocks.map((block, blockIndex) => {
        const lines = block.split('\n');
        const isList = lines.every((line) => line.trimStart().startsWith('- '));

        if (isList) {
          return (
            <ul key={blockIndex} className="my-3 list-disc space-y-1 ps-5 text-sm">
              {lines.map((line, lineIndex) => (
                <li key={lineIndex}>
                  {renderInline(line.trimStart().slice(2), `${blockIndex}-${lineIndex}`)}
                </li>
              ))}
            </ul>
          );
        }

        return (
          <p key={blockIndex} className="my-3 text-sm leading-relaxed">
            {lines.map((line, lineIndex) => (
              <Fragment key={lineIndex}>
                {lineIndex > 0 ? <br /> : null}
                {renderInline(line, `${blockIndex}-${lineIndex}`)}
              </Fragment>
            ))}
          </p>
        );
      })}
    </div>
  );
}
