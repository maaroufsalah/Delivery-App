import '@/theme/global.css';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { useState } from 'react';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { I18nProvider } from '@/i18n';
import { SessionProvider } from '@/lib/session';
import { UpdateProvider } from '@/lib/updates';
import { TenantThemeProvider } from '@/theme/tenant-theme';
import { AppGate } from '@/components/app-gate';
import { ThemedStatusBar } from '@/components/themed-status-bar';
import { PushRegistration } from '@/components/push-registration';
import { UpdateBanner } from '@/components/update-banner';
import { ToastProvider } from '@/ui';

/**
 * La racine — M1.
 *
 * L'ordre des fournisseurs n'est pas indifférent :
 *
 *  1. **i18n** en premier : tout le reste peut vouloir un message, y compris
 *     l'écran de mise à jour obligatoire.
 *  2. **Thème** ensuite : il appelle `/public/company`, donc il a besoin du
 *     slug mais pas d'une session.
 *  3. **Session** enfin : elle décide de l'espace, et `AppGate` l'oriente.
 */
export default function RootLayout() {
  /*
   * Le client de requêtes est créé DANS le composant, en état : le créer au
   * niveau du module le partagerait entre deux montages en développement, et
   * le cache d'un compte déconnecté survivrait au suivant.
   */
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            /*
             * Trente secondes : une tournée ne change pas toutes les
             * secondes, et un livreur en 3G ne doit pas payer un aller-retour
             * à chaque fois qu'il revient sur l'écran.
             */
            staleTime: 30_000,
            retry: 1,
            // Le rafraîchissement au retour d'écran est géré à la main, là où
            // il compte : l'automatique déclenche sur un simple changement de
            // clavier et consomme du réseau pour rien.
            refetchOnWindowFocus: false,
          },
        },
      }),
  );

  return (
    <SafeAreaProvider>
      <I18nProvider>
        <TenantThemeProvider>
          <QueryClientProvider client={queryClient}>
            <SessionProvider>
              {/*
                La barre système suit NOTRE schéma, pas celui de l'OS. `auto`
                la ferait claire sur un téléphone en sombre alors que
                l'application, elle, reste claire (C9 : pas de suivi du
                système) — et le texte de la barre deviendrait illisible.
              */}
              <ThemedStatusBar />
              {/*
                L'inscription aux notifications : DANS la session, parce
                qu'elle attend un profil, et hors d'AppGate parce qu'elle doit
                survivre aux changements d'espace (bloc 9). Elle ne rend rien.
              */}
              <PushRegistration />
              {/*
                Les toasts enveloppent les écrans — M4. Sous la session, parce
                qu'un message de succès suit presque toujours un geste
                authentifié ; au-dessus du Stack, pour survivre au changement
                d'écran que ce geste provoque.
              */}
              <ToastProvider>
                <AppGate>
                  {/*
                    LA VÉRIFICATION OTA vit ICI, sous le portail — C14 bloc 4.

                    Sous `AppGate` parce que celui-ci ne rend ses enfants
                    qu'une fois le splash passé : la vérification part donc au
                    moment où l'écran de connexion ou l'accueil s'affiche, et
                    le bandeau ne peut pas apparaître par-dessus un écran que
                    personne n'a encore vu.

                    Au-dessus du `Stack`, pour survivre au changement d'écran
                    — une mise à jour prête le reste quand on navigue.
                  */}
                  <UpdateProvider>
                    <UpdateBanner />
                    <Stack screenOptions={{ headerShown: false }} />
                  </UpdateProvider>
                </AppGate>
              </ToastProvider>
            </SessionProvider>
          </QueryClientProvider>
        </TenantThemeProvider>
      </I18nProvider>
    </SafeAreaProvider>
  );
}
