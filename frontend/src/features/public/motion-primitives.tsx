'use client';

import { motion, useReducedMotion } from 'motion/react';
import type { ReactNode } from 'react';

/**
 * Les deux seules primitives animées du site, et elles ne servent QUE la
 * landing. Aucune page authentifiée n'en importe : un agent qui ouvre trente
 * fois la liste des colis dans sa journée n'a rien à gagner à une apparition en
 * fondu, il a une tâche à finir.
 *
 * Règles tenues ici :
 *  - `whileInView` avec `once` : c'est un IntersectionObserver, pas un
 *    écouteur de scroll. Rien ne tourne entre deux franchissements.
 *  - `prefers-reduced-motion` coupe tout mouvement. Pas « réduit » : coupé.
 *    Le contenu est alors rendu à son état final, jamais masqué.
 *  - le mouvement se fait sur `y` uniquement, jamais sur `x` : une translation
 *    horizontale devrait être inversée en arabe, et un fondu vertical dit la
 *    même chose dans les deux sens d'écriture.
 *
 * `data-reveal` n'est pas décoratif : motion sérialise `opacity: 0` DANS le HTML
 * rendu au serveur. Sans JavaScript, ces sections resteraient invisibles. Une
 * règle `@media (scripting: none)` de globals.css s'accroche à cet attribut et
 * les rend à leur état final. Le contenu est donc lisible même sans JS — ce
 * n'est pas une page décorative, c'est la vitrine du produit.
 */

const EASE = [0.22, 1, 0.36, 1] as const;

export function Reveal({
  children,
  delay = 0,
  className,
  as = 'div',
}: {
  children: ReactNode;
  /** Décalage en secondes, pour échelonner une grille de cards. */
  delay?: number;
  className?: string;
  as?: 'div' | 'section' | 'li' | 'header';
}) {
  const reduced = useReducedMotion();
  const Component = motion[as];

  if (reduced) {
    return <Component className={className}>{children}</Component>;
  }

  return (
    <Component
      data-reveal
      className={className}
      initial={{ opacity: 0, y: 24 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, amount: 0.15, margin: '0px 0px -80px 0px' }}
      transition={{ duration: 0.55, delay, ease: EASE }}
    >
      {children}
    </Component>
  );
}

/**
 * Grille dont les enfants apparaissent l'un après l'autre.
 *
 * L'échelonnement est plafonné : au-delà de six cards, la dernière arriverait
 * si tard que le visiteur l'aurait déjà dépassée au scroll.
 */
export function RevealGroup({
  children,
  className,
  step = 0.07,
  as = 'div',
}: {
  children: ReactNode[];
  className?: string;
  step?: number;
  as?: 'div' | 'ul';
}) {
  const Component = as === 'ul' ? 'ul' : 'div';

  return (
    <Component className={className}>
      {children.map((child, index) => (
        <Reveal key={index} as={as === 'ul' ? 'li' : 'div'} delay={Math.min(index, 5) * step}>
          {child}
        </Reveal>
      ))}
    </Component>
  );
}
