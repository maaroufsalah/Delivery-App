import Constants from 'expo-constants';
import { useRouter } from 'expo-router';
import {
  Banknote,
  Bell,
  Building2,
  ChevronRight,
  LogOut,
  MessageSquare,
  RotateCcw,
  Settings,
  User,
} from 'lucide-react-native';
import type { LucideIcon } from 'lucide-react-native';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { useSession } from '@/lib/session';
import { useTenantTheme } from '@/theme/tenant-theme';
import { Card, Divider, Screen } from '@/ui';

/**
 * « PLUS » — V2 vendeur bloc 1, spécification §12.
 *
 * **Le cinquième onglet demandé par Anass**, et le même geste que chez le
 * livreur (L2 bloc 4) : quatre destinations de travail dans la barre, un
 * tiroir pour le reste. La barre servait jusqu'ici Accueil, Colis, « Créer »,
 * Finance et Profil ; la spécification veut Accueil, Colis, Bons, Warehouse et
 * Plus, et « Créer » redevient ce qu'il est — une ACTION, pas une destination.
 *
 * **Une page, pas une feuille.** Une feuille se ferme d'un geste à côté, ce qui
 * convient à un choix rapide ; ici on vient chercher un écran, et on veut
 * pouvoir revenir.
 *
 * **Il ne liste que ce qui EXISTE.** Le centre de notifications arrive au bloc
 * 8, et il ajoutera sa ligne en même temps que son écran : une entrée qui mène
 * à une route absente fait planter le routeur, et une entrée grisée fait douter
 * de l'application entière.
 */
export default function MerchantMore() {
  const { t } = useI18n();
  const router = useRouter();
  const { signOut } = useSession();
  const { brand } = useTenantTheme();

  return (
    <Screen>
      <Text className="px-5 pb-3 pt-4 text-[22px] font-bold text-foreground">
        {t('merchantMore.title')}
      </Text>

      <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-3 px-5 pb-8">
        {/* --- L'argent : ce qu'on vient consulter, pas manipuler --- */}
        <Card className="gap-1 p-1">
          <Row
            icon={Banknote}
            label={t('finance.title')}
            onPress={() => router.push('/(merchant)/finance')}
          />
          <Divider />
          <Row
            icon={RotateCcw}
            label={t('pickup.title')}
            onPress={() => router.push('/(merchant)/pickup')}
          />
        </Card>

        {/* --- Ce qui ne va pas, et ce qu'on doit savoir --- */}
        <Card className="gap-1 p-1">
          <Row
            icon={MessageSquare}
            label={t('claims.title')}
            onPress={() => router.push('/(merchant)/claims')}
          />
          <Divider />
          {/* Le centre de notifications — V2 bloc 8. Il rejoint le tiroir avec
              son écran, comme les réclamations l'avaient fait en L2. */}
          <Row
            icon={Bell}
            label={t('notifications.title')}
            onPress={() => router.push('/(merchant)/notifications')}
          />
        </Card>

        {/* --- Moi --- */}
        <Card className="gap-1 p-1">
          <Row
            icon={User}
            label={t('menu.profile')}
            onPress={() => router.push('/(merchant)/profile')}
          />
          <Divider />
          {/* « Ma société » — V2 bloc 9. Ces champs n'étaient éditables que par
              l'administration : un vendeur qui déménageait devait téléphoner. */}
          <Row
            icon={Building2}
            label={t('company.title')}
            onPress={() => router.push('/(merchant)/company')}
          />
          <Divider />
          <Row
            icon={Settings}
            label={t('settings.title')}
            onPress={() => router.push('/(merchant)/settings')}
          />
        </Card>

        <Card className="gap-1 p-1">
          <Row icon={LogOut} label={t('profile.logout')} danger onPress={() => void signOut()} />
        </Card>

        {/*
          Le pied porte la société et la version : c'est ce qu'on demande au
          téléphone quand quelque chose ne va pas, et le chercher dans les
          réglages du système prend deux minutes.
        */}
        <View className="items-center gap-0.5 pt-2">
          <Text className="text-[13px] text-muted-foreground">{brand?.name ?? ''}</Text>
          <Text className="text-[12px] text-muted-foreground">
            {t('menu.version', { version: Constants.expoConfig?.version ?? '—' })}
          </Text>
        </View>
      </ScrollView>
    </Screen>
  );
}

/** Une ligne de tiroir : icône, libellé, chevron, 52 points de haut. */
function Row({
  icon: Icon,
  label,
  danger,
  onPress,
}: {
  icon: LucideIcon;
  label: string;
  danger?: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      onPress={onPress}
      className="min-h-[52px] flex-row items-center gap-3 rounded-2xl px-3"
      style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
    >
      <Icon size={20} className={danger ? 'text-destructive' : 'text-muted-foreground'} />
      <Text
        className={`flex-1 text-[16px] ${danger ? 'text-destructive' : 'text-foreground'}`}
        numberOfLines={1}
      >
        {label}
      </Text>
      {danger ? null : <ChevronRight size={18} className="flip-rtl text-muted-foreground" />}
    </Pressable>
  );
}
