import { getTranslations } from 'next-intl/server';

/** Une section rendue, résolue par la page qui la possède. */
export interface LegalSection {
  key: string;
  title: string;
  body: string;
}

/**
 * Un document légal, rendu depuis le catalogue — M1 bloc 9.
 *
 * **Deux pages, un seul composant.** `/privacy` et `/terms` ont la même forme :
 * un titre, une date, un chapeau, des sections numérotées, un contact. Ce qui
 * change, c'est le contenu — et le contenu vit dans `messages/`, comme tout le
 * reste (règle d'architecture n°4 : aucune chaîne visible en dur).
 *
 * **Les sections arrivent RÉSOLUES, pas sous forme de clés.** Chaque page a ses
 * propres sections ; un `t(`${doc}.sections.${clé}`)` construit ici formerait le
 * produit croisé des deux documents, dont la moitié des clés n'existe pas — et
 * le typage des messages a raison de le refuser. La page, elle, connaît son
 * espace de noms littéral et ses clés : elle résout, ce composant met en forme.
 *
 * **L'ordre des sections est dans les pages, pas dans le JSON.** Un objet JSON
 * n'a pas d'ordre garanti à la lecture, et une politique de confidentialité dont
 * les paragraphes changeraient de place d'un build à l'autre ne serait pas un
 * document.
 *
 * **Pas de composant client, pas de `Card`.** Un texte que des relecteurs de
 * magasins ouvrent sans JavaScript, et qu'un moteur doit pouvoir indexer, n'a
 * besoin ni de l'un ni de l'autre.
 */
export async function LegalDocument({
  doc,
  sections,
}: {
  doc: 'privacy' | 'terms';
  sections: readonly LegalSection[];
}) {
  const t = await getTranslations('legal');

  return (
    <article className="mx-auto w-full max-w-3xl px-4 py-14 sm:px-6 sm:py-20">
      <header className="border-b pb-6">
        <h1 className="text-3xl font-semibold tracking-tight text-balance">{t(`${doc}.title`)}</h1>
        <p className="text-muted-foreground mt-2 text-sm">{t('updated')}</p>
        <p className="mt-4 text-base leading-relaxed text-pretty">{t(`${doc}.lead`)}</p>
      </header>

      <div className="mt-8 flex flex-col gap-8">
        {sections.map((section, index) => (
          <section key={section.key}>
            <h2 className="text-lg font-semibold">
              {/* Le numéro est décoratif : un lecteur d'écran annonce déjà le
                  titre, et le répéter en chiffres ne lui apprendrait rien. */}
              <span className="text-muted-foreground me-2 tabular-nums" aria-hidden>
                {index + 1}.
              </span>
              {section.title}
            </h2>
            <p className="mt-2 leading-relaxed text-pretty">{section.body}</p>
          </section>
        ))}
      </div>

      <footer className="mt-10 border-t pt-6">
        <h2 className="text-lg font-semibold">{t('contactTitle')}</h2>
        <p className="mt-2 leading-relaxed text-pretty">{t('contactBody')}</p>
      </footer>
    </article>
  );
}
