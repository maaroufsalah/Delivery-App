import type { ReactNode } from 'react';
import { Redirect } from 'expo-router';
import { type Space, useSession } from '@/lib/session';
import { Splash } from '@/components/splash';

/**
 * UN ESPACE N'OUVRE QUE POUR SON RÔLE — étanchéité vendeur, point 2.
 *
 * L'aiguillage vit à la racine (`app/index.tsx`) : il envoie chacun chez lui,
 * mais rien n'empêchait d'ENTRER ailleurs. Un lien profond
 * (`nocidodelivery://(staff)/home`) ou une notification mal routée ouvrait au
 * vendeur l'accueil du staff, et ses alertes. Le serveur refuse les lectures ;
 * l'écran, lui, ne doit même pas se monter.
 *
 * Chaque layout d'espace s'enveloppe de cette garde : un compte qui n'est pas
 * du bon espace repart à la racine, qui le renvoie chez lui.
 */
export function SpaceGuard({ space, children }: { space: Space; children: ReactNode }) {
  const session = useSession();

  if (session.loading) return <Splash />;
  if (session.space !== space) return <Redirect href="/" />;

  return <>{children}</>;
}
