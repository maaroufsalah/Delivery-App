'use client';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState } from 'react';

export function QueryProvider({ children }: { children: React.ReactNode }) {
  const [client] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            /*
             * Les lectures partent du SERVEUR (D-018) : ce client ne sert que
             * le peu d'état interactif qui reste côté navigateur. Une minute de
             * fraîcheur et pas de rechargement au retour d'onglet — recharger
             * une liste parce qu'on a changé de fenêtre n'apprend rien à
             * personne et refait un aller-retour.
             */
            staleTime: 60_000,
            refetchOnWindowFocus: false,
            retry: 1,
          },
        },
      }),
  );

  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}
