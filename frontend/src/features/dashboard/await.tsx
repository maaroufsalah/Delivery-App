import type { ReactNode } from 'react';

/**
 * ATTENDRE UNE PROMESSE DANS L'ARBRE — chantier « Dashboard de pilotage ».
 *
 * Composant SERVEUR asynchrone : il attend la promesse qu'on lui donne et rend
 * ce que la fonction enfant en fait. Posé sous un `<Suspense>`, il laisse le
 * reste de la page partir au navigateur pendant que l'agrégat se calcule ; sous
 * une frontière d'erreur, une promesse rejetée ne fait tomber que sa section.
 *
 * La promesse est CRÉÉE par la page, une fois, et peut être attendue par
 * plusieurs sections : la vue d'ensemble nourrit les KPI, les alertes et les
 * derniers colis sans être demandée trois fois.
 */
export async function Await<T>({
  promise,
  children,
}: {
  promise: Promise<T>;
  children: (value: T) => ReactNode;
}) {
  const value = await promise;
  return <>{children(value)}</>;
}
