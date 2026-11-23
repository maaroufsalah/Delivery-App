'use client';

import { useEffect } from 'react';

/**
 * « MODIFICATIONS NON ENREGISTRÉES » — lot N2 (D-225, option A).
 *
 * Tant que `dirty` est vrai, quitter la page demande confirmation :
 *
 *  - fermer l'onglet, recharger, taper une adresse : `beforeunload` (le
 *    navigateur montre SON message, le nôtre n'est pas affiché) ;
 *  - un lien de l'application (menu, fil d'Ariane, `<Link>`) : un clic écouté
 *    en CAPTURE sur le document, avant que React ne lance la navigation —
 *    Next n'offre pas de garde de navigation ; refuser arrête le clic.
 *
 * Le retour arrière du navigateur n'est pas retenu (pas d'API fiable pour le
 * suspendre) : c'est le seul chemin sans avertissement.
 */
export function useUnsavedChanges(dirty: boolean, message: string): void {
  useEffect(() => {
    if (!dirty) return;

    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      // Chrome exige encore une valeur pour montrer la boîte.
      event.returnValue = '';
    };

    const onClick = (event: MouseEvent) => {
      if (event.defaultPrevented || event.button !== 0) return;
      // Ctrl/⌘-clic ou Maj-clic : un nouvel onglet, la page reste.
      if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      const anchor = (event.target as Element | null)?.closest?.('a[href]');
      if (!(anchor instanceof HTMLAnchorElement)) return;
      if (anchor.target && anchor.target !== '_self') return;
      if (anchor.hasAttribute('download')) return;

      const next = new URL(anchor.href, window.location.href);
      // Une ancre de la même page ne quitte rien.
      if (
        next.origin === window.location.origin &&
        next.pathname === window.location.pathname &&
        next.search === window.location.search
      ) {
        return;
      }
      if (!window.confirm(message)) {
        event.preventDefault();
        event.stopPropagation();
      }
    };

    window.addEventListener('beforeunload', onBeforeUnload);
    document.addEventListener('click', onClick, true);
    return () => {
      window.removeEventListener('beforeunload', onBeforeUnload);
      document.removeEventListener('click', onClick, true);
    };
  }, [dirty, message]);
}
