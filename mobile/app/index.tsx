import { Redirect } from 'expo-router';
import { IS_SINGLE_TENANT } from '@/config/brand';
import { useSession } from '@/lib/session';
import { Splash } from '@/components/splash';

/**
 * L'aiguillage — M1, D-072.
 *
 * Il ne rend jamais d'écran : il redirige vers l'espace du rôle. Les trois
 * groupes existent dès M1 pour que les deux phases suivantes n'aient qu'à
 * remplir le leur — `(merchant)` et `(staff)` affichent « Bientôt » et rien
 * d'autre.
 *
 * `none` renvoie à la connexion : un compte sans rattachement — un
 * administrateur de plateforme, par exemple — n'a rien à faire ici, et un
 * écran vide serait plus déroutant qu'un retour en arrière.
 *
 * **Mono-client (C15 bloc 4)** : c'est l'écran de CONNEXION qui accueille, et
 * non plus « Votre société ». Le tenant est une constante de build, il n'y a
 * plus rien à demander. La variante multi-client garde son premier écran.
 */
export default function Index() {
  const { space, loading } = useSession();

  if (loading) return <Splash />;

  switch (space) {
    case 'courier':
      /*
       * L'ACCUEIL, et non le scanner — M4 bloc 2.
       *
       * M1 ouvrait sur la caméra : l'écran du geste du matin, mais pas un
       * point de départ. Il n'annonce rien, et il allume l'appareil photo de
       * quelqu'un qui voulait seulement savoir ce qu'il lui reste à livrer.
       */
      return <Redirect href="/(courier)/home" />;
    case 'merchant':
      return <Redirect href="/(merchant)/home" />;
    case 'staff':
      return <Redirect href="/(staff)/home" />;
    default:
      return <Redirect href={IS_SINGLE_TENANT ? '/(auth)/login' : '/(auth)/company'} />;
  }
}
