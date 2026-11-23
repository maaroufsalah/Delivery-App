import { LayoutDashboard, ScanLine, Search, User } from 'lucide-react-native';
import { Tabs } from 'expo-router';
import { useI18n } from '@/i18n';
import { OfflineBanner } from '@/components/offline-banner';
import { roleTabBar } from '@/components/role-tab-bar';
import { SpaceGuard } from '@/components/space-guard';

/**
 * L'espace du STAFF — M3.
 *
 * Quatre onglets, et c'est le plus court des trois espaces. Un agent d'agence
 * a un ordinateur : le téléphone ne refait pas le back-office, il sert les
 * gestes qui se font DEBOUT — voir ce qui cloche, retrouver un colis, le
 * scanner au comptoir.
 *
 * Les écrans de traitement — ramassages, remises, réclamations — s'atteignent
 * depuis les alertes qui les annoncent, parce qu'on y va toujours à cause de
 * quelque chose.
 *
 * **M4 y pose la barre commune** : le scan au centre, en relief, parce que
 * c'est le geste du comptoir. Quatre entrées, elles n'ont pas changé.
 */
export default function StaffLayout() {
  const { t } = useI18n();

  return (
    <SpaceGuard space="staff">
      <OfflineBanner />

      <Tabs
        backBehavior="fullHistory"
        screenOptions={{ headerShown: false }}
        tabBar={roleTabBar([
          { name: 'home', label: t('staff.home'), icon: LayoutDashboard },
          { name: 'scan', label: t('scan.title'), icon: ScanLine, raised: true },
          { name: 'search', label: t('staff.search'), icon: Search },
          { name: 'profile', label: t('profile.title'), icon: User },
        ])}
      >
        <Tabs.Screen name="home" />
        <Tabs.Screen name="scan" />
        <Tabs.Screen name="search" />
        <Tabs.Screen name="profile" />

        {/* Atteints depuis les alertes, hors de la barre. */}
        <Tabs.Screen name="parcel/[code]" options={{ href: null }} />
        <Tabs.Screen name="pickups" options={{ href: null }} />
        <Tabs.Screen name="remittances" options={{ href: null }} />
        <Tabs.Screen name="claims" options={{ href: null }} />
        <Tabs.Screen name="claim/[code]" options={{ href: null }} />
        <Tabs.Screen name="soon" options={{ href: null }} />
      </Tabs>
    </SpaceGuard>
  );
}
