import { useFocusEffect } from 'expo-router';
import { useCallback, useEffect, useRef } from 'react';

/**
 * RELIRE LE SERVEUR QUAND L'ÉCRAN REVIENT SOUS LES YEUX — chantier « Mobile :
 * Retour + circuits clôturés ».
 *
 * Le cache global ne redemande rien au focus (`refetchOnWindowFocus: false`,
 * une 3G d'agence), et c'est voulu pour les écrans qui ne changent pas. Les
 * tournées, elles, changent SANS le livreur : l'agence clôt un circuit, valide
 * un bon, en assigne un autre. Sans relecture, le circuit clos restait dans
 * la liste jusqu'au prochain tirer-pour-rafraîchir, et le livreur pouvait
 * l'ouvrir. Chaque écran qui montre des tournées relit donc à CHAQUE retour.
 *
 * Les objets de requête changent d'identité à chaque rendu ; on lit toujours
 * les derniers, sans relancer l'effet pour autant.
 */
export function useRefetchOnFocus(...queries: { refetch: () => unknown }[]): void {
  const latest = useRef(queries);
  // Mis à jour après chaque rendu, jamais pendant (règle `react-hooks/refs`).
  useEffect(() => {
    latest.current = queries;
  });

  useFocusEffect(
    useCallback(() => {
      for (const query of latest.current) void query.refetch();
    }, []),
  );
}
