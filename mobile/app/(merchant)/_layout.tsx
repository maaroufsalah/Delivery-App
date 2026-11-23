import { Boxes, ClipboardList, Home, MoreHorizontal, Package } from 'lucide-react-native';
import { Tabs } from 'expo-router';
import { useI18n } from '@/i18n';
import { OfflineBanner } from '@/components/offline-banner';
import { MerchantQuickActions } from '@/components/merchant-quick-actions';
import { roleTabBar } from '@/components/role-tab-bar';
import { SpaceGuard } from '@/components/space-guard';

/**
 * L'espace de l'E-COMMERÇANT — M2, refondu au V2 vendeur bloc 1.
 *
 * **La barre suit désormais la spécification d'Anass (§12)** :
 * `Accueil · Colis · Bons · Warehouse · Plus`. M4 avait mis « Créer un colis »
 * au centre, en relief, à la place du scan du livreur ; c'était juste tant que
 * le vendeur n'avait que trois écrans. Avec les bons et l'entrepôt, garder une
 * ACTION dans la barre aurait coûté un des cinq pouces à quelque chose qui
 * n'est pas un lieu.
 *
 * « Créer » n'est pas perdu pour autant : il passe dans le bouton « + », qui
 * flotte au-dessus de la barre sur tous les écrans de l'espace et porte les
 * six actions rapides de la spécification (§16).
 *
 * **L'accueil en premier.** C'est l'écran qu'on ouvre entre deux commandes pour
 * une seule question : qu'est-ce qui s'est passé aujourd'hui, et combien me
 * doit-on. Les colis viennent ensuite, parce qu'on y va quand on cherche
 * quelque chose de précis.
 */
export default function MerchantLayout() {
  const { t } = useI18n();

  return (
    <SpaceGuard space="merchant">
      {/* La file hors-ligne concerne aussi ce rôle : la création d'un colis y
          passe quand le réseau manque (D-077). */}
      <OfflineBanner />

      <Tabs
        backBehavior="fullHistory"
        screenOptions={{ headerShown: false }}
        tabBar={roleTabBar([
          { name: 'home', label: t('merchant.home'), icon: Home },
          { name: 'parcels', label: t('merchant.parcels'), icon: Package },
          { name: 'notes', label: t('notes.title'), icon: ClipboardList },
          { name: 'warehouse', label: t('warehouse.title'), icon: Boxes },
          { name: 'more', label: t('merchantMore.title'), icon: MoreHorizontal },
        ])}
      >
        <Tabs.Screen name="home" />
        <Tabs.Screen name="parcels" />
        <Tabs.Screen name="notes" />
        <Tabs.Screen name="warehouse" />
        <Tabs.Screen name="more" />

        {/*
          Hors de la barre, atteints depuis les écrans qui les appellent ou
          depuis le bouton « + ».

          CINQ onglets au plus : au-delà, les libellés se tronquent et les
          cibles passent sous 44 points. La création part du « + » et de
          « Mes colis » ; les réclamations partent d'un colis, parce qu'on en
          ouvre une À PROPOS de quelque chose ; la finance et le profil vivent
          dans « Plus ».
        */}
        <Tabs.Screen name="new" options={{ href: null }} />
        <Tabs.Screen name="finance" options={{ href: null }} />
        <Tabs.Screen name="profile" options={{ href: null }} />
        <Tabs.Screen name="settings" options={{ href: null }} />
        <Tabs.Screen name="company" options={{ href: null }} />
        <Tabs.Screen name="parcel/[code]" options={{ href: null }} />
        {/* Recette 3 point 3 : la correction d'un colis avant ramassage. */}
        <Tabs.Screen name="parcel-edit" options={{ href: null }} />
        <Tabs.Screen name="note/[code]" options={{ href: null }} />
        <Tabs.Screen name="product/[id]" options={{ href: null }} />
        <Tabs.Screen name="product/new" options={{ href: null }} />
        <Tabs.Screen name="stock/[id]" options={{ href: null }} />
        <Tabs.Screen name="pickup" options={{ href: null }} />
        <Tabs.Screen name="claims" options={{ href: null }} />
        <Tabs.Screen name="notifications" options={{ href: null }} />
        <Tabs.Screen name="claim/[code]" options={{ href: null }} />
        <Tabs.Screen name="claim/new" options={{ href: null }} />
        {/* L'écran « Bientôt » de M1 n'a plus lieu d'être, mais la route reste
            tant qu'aucun écran ne l'a remplacée. */}
        <Tabs.Screen name="soon" options={{ href: null }} />
      </Tabs>

      {/* Au-dessus de la barre, sous les feuilles : le « + » de la
          spécification §16. Monté ICI pour être servi par tous les écrans de
          l'espace sans que chacun ait à le rendre. */}
      <MerchantQuickActions />
    </SpaceGuard>
  );
}
