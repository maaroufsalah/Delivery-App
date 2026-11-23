import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * TIRER POUR RAFRAÎCHIR, ET SEULEMENT ÇA — Recette 7, point 6 (audit MOB-11).
 *
 * `refreshing={query.isRefetching}` allumait la roue à CHAQUE relecture
 * silencieuse — retour sur l'écran (`useRefetchOnFocus`), invalidation après
 * une action, reprise du réseau. Sur iOS, l'animation de décalage qui
 * l'accompagne absorbe le premier appui ; sur Android, la liste saute. Le
 * livreur appuyait deux fois.
 *
 * Ici, la roue ne tourne que pour un geste DU LIVREUR : elle s'allume quand il
 * tire, s'éteint quand les relectures qu'il a demandées sont finies — toutes,
 * et quoi qu'il arrive (`finally`). Les relectures silencieuses continuent
 * d'exister ; elles ne se voient plus.
 */
export function usePullToRefresh(...queries: { refetch: () => Promise<unknown> }[]): {
  refreshing: boolean;
  refresh: () => void;
} {
  const [refreshing, setRefreshing] = useState(false);
  const mounted = useRef(true);
  const latest = useRef(queries);
  useEffect(() => {
    latest.current = queries;
  });
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  const refresh = useCallback(() => {
    setRefreshing(true);
    void Promise.allSettled(latest.current.map((query) => query.refetch())).finally(() => {
      if (mounted.current) setRefreshing(false);
    });
  }, []);

  return { refreshing, refresh };
}
