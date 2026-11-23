import { Tabs } from 'expo-router';
import { Home, MoreHorizontal, ScanLine } from 'lucide-react-native';
import { useI18n } from '@/i18n';
import { OfflineBanner } from '@/components/offline-banner';
import { roleTabBar } from '@/components/role-tab-bar';
import { SpaceGuard } from '@/components/space-guard';

/**
 * L'ESPACE DU LIVREUR — M1 (D-068), la coque refaite par M4, la barre
 * simplifiée par le chantier « Accueil et menu bas livreur simplifiés »
 * (`docs/design/livreur-accueil-v2.md`).
 *
 * **Les routes restent des onglets `expo-router`** : l'état de chaque écran
 * survit au changement d'onglet, et les liens profonds continuent de marcher.
 * **`backBehavior="fullHistory"`** — chantier « Mobile : Retour + circuits
 * clôturés ». Le défaut d'un navigateur à onglets est `firstRoute` : « retour »
 * (la flèche comme le bouton Android) sautait TOUJOURS au premier onglet,
 * l'accueil, quel que soit l'écran d'où l'on venait. Avec l'historique
 * complet, le retour rend l'écran précédent avec ses paramètres (la fiche
 * colis d'avant, pas une autre) et son état (filtres, défilement) ; un
 * `replace` retire l'écran remplacé de l'historique. Seule la BARRE est à
 * nous, par `tabBar` — c'est ce qui permet le bouton central en relief, les
 * badges numériques et l'haptique, qu'aucune option de `Tabs` ne propose.
 *
 * **Trois destinations, et non plus cinq** : Accueil, Scanner, Plus. Colis,
 * Circuits et Ramassages quittent la barre mais PAS le routeur — ce sont
 * toujours des `Tabs.Screen`, à la même route, avec le même écran ; on y entre
 * par les tuiles de l'accueil, et le retour ramène là d'où l'on vient.
 * Le scanner revient au centre, en relief, à 72 points : c'est le geste du
 * matin, et il se trouve sans lire.
 *
 * **Aucun changement fonctionnel** : chaque écran garde sa route et au moins
 * une porte d'entrée (tableau ancien → nouvel accès dans le chantier).
 */
export default function CourierLayout() {
  const { t } = useI18n();

  return (
    <SpaceGuard space="courier">
      {/* Au-dessus des onglets : l'état de la file se voit de partout (D-077). */}
      <OfflineBanner />

      <Tabs
        backBehavior="fullHistory"
        screenOptions={{ headerShown: false }}
        tabBar={roleTabBar(
          [
            { name: 'home', label: t('home.title'), icon: Home },
            { name: 'scan', label: t('scan.title'), icon: ScanLine, raised: true },
            { name: 'more', label: t('more.title'), icon: MoreHorizontal },
          ],
          { raisedSize: 72 },
        )}
      >
        <Tabs.Screen name="home" />
        <Tabs.Screen name="scan" />
        <Tabs.Screen name="more" />

        {/*
          Les écrans qui ne sont plus dans la barre, et ceux de détail : ils
          restent des routes du même navigateur — `href: null` les retire de
          la barre sans les retirer du routeur.
        */}
        <Tabs.Screen name="parcels" options={{ href: null }} />
        <Tabs.Screen name="circuits" options={{ href: null }} />
        {/* « Stock » : tous les colis à livrer en une liste, tuile d'accueil. */}
        <Tabs.Screen name="stock" options={{ href: null }} />
        <Tabs.Screen name="pickups" options={{ href: null }} />
        <Tabs.Screen name="returns" options={{ href: null }} />
        <Tabs.Screen name="claims" options={{ href: null }} />
        <Tabs.Screen name="claim/[code]" options={{ href: null }} />
        <Tabs.Screen name="claim/new" options={{ href: null }} />
        <Tabs.Screen name="billing" options={{ href: null }} />
        <Tabs.Screen name="statuses" options={{ href: null }} />
        <Tabs.Screen name="settings" options={{ href: null }} />
        <Tabs.Screen name="account" options={{ href: null }} />
        <Tabs.Screen name="profile" options={{ href: null }} />
        <Tabs.Screen name="parcel/[code]" options={{ href: null }} />
        <Tabs.Screen name="cheque" options={{ href: null }} />
        <Tabs.Screen name="remittances" options={{ href: null }} />
        {/* D-168 — la petite caisse du livreur, tuile d'accueil et menu Plus. */}
        <Tabs.Screen name="petty-cash" options={{ href: null }} />
        <Tabs.Screen name="petty-cash-form" options={{ href: null }} />
        <Tabs.Screen name="statements" options={{ href: null }} />
        <Tabs.Screen name="stats" options={{ href: null }} />
        <Tabs.Screen name="bl" options={{ href: null }} />
        <Tabs.Screen name="circuit/[code]" options={{ href: null }} />
        <Tabs.Screen name="navigate/[code]" options={{ href: null }} />
        <Tabs.Screen name="note/[id]" options={{ href: null }} />
        <Tabs.Screen name="handover/new" options={{ href: null }} />
        <Tabs.Screen name="handover/[code]" options={{ href: null }} />
      </Tabs>
    </SpaceGuard>
  );
}
