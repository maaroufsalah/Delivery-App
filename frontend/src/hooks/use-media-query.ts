'use client';

import { useCallback, useSyncExternalStore } from 'react';

/**
 * Un point de rupture, lisible depuis React — C10 bloc 5.
 *
 * `useSyncExternalStore` plutôt qu'un `useEffect` qui poserait un état
 * (D-011) : l'abonnement et la lecture sont le même contrat, et React sait
 * qu'il lit une source externe.
 *
 * **Le `serverSnapshot` n'est pas neutre, et c'est délibéré.** Il vaut ce que
 * l'appelant lui donne, parce que le bon pari dépend de ce qu'on sait : la
 * barre latérale ne peut être repliée que depuis un écran large — le bouton
 * n'existe pas ailleurs. Un cookie qui dit « repliée » vient donc forcément
 * d'un écran large, et parier « large » au rendu serveur rend le premier
 * pixel juste dans le cas courant. Une fenêtre rétrécie entre deux visites se
 * corrige après hydratation, une fois, au lieu de faire clignoter tout le
 * monde à chaque chargement.
 */
export function useMediaQuery(query: string, serverSnapshot = false): boolean {
  const subscribe = useCallback(
    (onChange: () => void) => {
      if (typeof window === 'undefined') return () => {};

      const list = window.matchMedia(query);
      list.addEventListener('change', onChange);
      return () => list.removeEventListener('change', onChange);
    },
    [query],
  );

  const getSnapshot = useCallback(() => window.matchMedia(query).matches, [query]);

  return useSyncExternalStore(subscribe, getSnapshot, () => serverSnapshot);
}
