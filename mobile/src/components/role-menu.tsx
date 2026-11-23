import Constants from 'expo-constants';
import { useRouter } from 'expo-router';
import {
  Banknote,
  Globe,
  LogOut,
  MessageSquare,
  PackageCheck,
  ShieldCheck,
  Truck,
  User,
} from 'lucide-react-native';
import { Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { useSession } from '@/lib/session';
import { useTenantTheme } from '@/theme/tenant-theme';
import { Divider, Sheet, SheetItem } from '@/ui';

/**
 * LE MENU GÉNÉRAL des espaces client et agence — M4 bloc 5.
 *
 * Le pendant de `CourierMenu`, pour les deux autres rôles. Il n'est pas
 * partagé avec lui parce que les entrées ne se recoupent qu'à moitié, et
 * qu'une fonction qui prendrait les deux jeux en paramètre serait plus longue
 * que les deux écrites côte à côte.
 *
 * **Chaque rôle n'y voit que ce que ses permissions lui ouvrent.**
 * L'e-commerçant porte `claims.read` et `claims.write` (il conteste ses
 * propres colis), le staff les porte aussi pour les traiter — le livreur, non,
 * et c'est pour cela que son menu n'a pas cette entrée.
 *
 * **Le pied porte la société et la version.** C'est ce qu'on demande au
 * téléphone quand quelque chose ne va pas.
 */
export function RoleMenu({
  open,
  onClose,
  space,
}: {
  open: boolean;
  onClose: () => void;
  space: 'merchant' | 'staff';
}) {
  const { t } = useI18n();
  const router = useRouter();
  const { signOut } = useSession();
  const { brand } = useTenantTheme();

  const go = (path: string) => {
    onClose();
    router.push(path as never);
  };

  return (
    <Sheet open={open} onClose={onClose} title={t('menu.title')}>
      {space === 'merchant' ? (
        <>
          <SheetItem
            icon={Truck}
            label={t('pickup.title')}
            onPress={() => go('/(merchant)/pickup')}
          />
          <SheetItem
            icon={MessageSquare}
            label={t('claims.title')}
            onPress={() => go('/(merchant)/claims')}
          />
        </>
      ) : (
        <>
          <SheetItem
            icon={PackageCheck}
            label={t('staff.pendingNotes')}
            onPress={() => go('/(staff)/pickups')}
          />
          <SheetItem
            icon={Banknote}
            label={t('remittances.title')}
            onPress={() => go('/(staff)/remittances')}
          />
          <SheetItem
            icon={MessageSquare}
            label={t('claims.title')}
            onPress={() => go('/(staff)/claims')}
          />
        </>
      )}

      <Divider />

      <SheetItem
        icon={User}
        label={t('menu.profile')}
        onPress={() => go(space === 'merchant' ? '/(merchant)/profile' : '/(staff)/profile')}
      />
      <SheetItem
        icon={Globe}
        label={t('profile.language')}
        hint={t('menu.inProfile')}
        onPress={() => go(space === 'merchant' ? '/(merchant)/profile' : '/(staff)/profile')}
      />
      <SheetItem
        icon={ShieldCheck}
        label={t('password.title')}
        hint={t('menu.inProfile')}
        onPress={() => go(space === 'merchant' ? '/(merchant)/profile' : '/(staff)/profile')}
      />

      <Divider />

      <SheetItem
        icon={LogOut}
        label={t('profile.logout')}
        tone="danger"
        onPress={() => {
          onClose();
          void signOut();
        }}
      />

      <View className="items-center gap-0.5 px-3 pb-1 pt-3">
        <Text className="text-[13px] text-muted-foreground">{brand?.name ?? ''}</Text>
        <Text className="text-[12px] text-muted-foreground">
          {t('menu.version', { version: Constants.expoConfig?.version ?? '—' })}
        </Text>
      </View>
    </Sheet>
  );
}
