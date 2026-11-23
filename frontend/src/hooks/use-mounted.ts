'use client';

import { useSyncExternalStore } from 'react';

const subscribe = () => () => {};
const getSnapshot = () => true;
const getServerSnapshot = () => false;

/**
 * `false` au rendu serveur et au premier rendu client, `true` ensuite.
 *
 * Nécessaire pour tout ce qui dépend du thème résolu (next-themes) : la valeur
 * n'est connue qu'après hydratation. Écrit avec `useSyncExternalStore` plutôt
 * qu'avec `useEffect(() => setState(true))`, qui déclenche un rendu en cascade.
 */
export function useMounted(): boolean {
  return useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
}
