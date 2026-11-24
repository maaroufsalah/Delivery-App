import AsyncStorage from '@react-native-async-storage/async-storage';
import * as LocalAuthentication from 'expo-local-authentication';
import { useRouter } from 'expo-router';
import { Fingerprint, LogOut, ShieldAlert, Trash2 } from 'lucide-react-native';
import { useEffect, useState } from 'react';
import { Alert, Linking, Pressable, ScrollView, Switch, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useI18n } from '@/i18n';
import { describeActionError } from '@/lib/use-action';
import { useSession } from '@/lib/session';
import { forgetSlug } from '@/lib/tenant';
import { IS_SINGLE_TENANT } from '@/config/brand';
import { PasswordCard } from '@/components/password-card';
import { SettingsControls } from '@/components/settings-controls';
import { UpdateCard } from '@/components/update-card';
import { Button, Card } from '@/ui';

const BIOMETRIC_KEY = 'app.biometric';

/** Les deux pages légales, exigées par les magasins, servies par la plateforme. */
const LEGAL = {
  privacy: 'https://new.wimodelivery.ma/fr/privacy',
  terms: 'https://new.wimodelivery.ma/fr/terms',
};

/**
 * LE PROFIL, partagé par les trois espaces — M2/M3.
 *
 * **Extrait, pas dupliqué.** Langue, verrou biométrique, pages légales,
 * déconnexion et suppression de compte ne dépendent pas du métier : ce sont les
 * réglages du COMPTE. En recopier deux cent lignes par espace aurait garanti
 * qu'un correctif de conformité n'atteigne qu'un des trois — et c'est la
 * suppression de compte, exigée par les deux magasins (D-071), qui en aurait
 * fait les frais.
 *
 * Chaque espace passe ce qui lui est propre : le livreur sa ville et son
 * agence, le marchand rien de plus que son nom.
 *
 * **La suppression demande une confirmation qui DIT ce qu'elle fait** : le
 * compte est anonymisé, les livraisons restent dans l'historique comptable de
 * l'agence. Un compte qui croit effacer ses courses et découvre qu'elles sont
 * toujours là aurait raison de se sentir trompé.
 */
export function ProfileScreen({
  subtitle,
  settings = true,
}: {
  subtitle?: string | null;
  /** `false` quand l'espace a son propre écran Paramètres — le livreur. */
  settings?: boolean;
}) {
  const { t } = useI18n();
  const router = useRouter();
  const { profile, signOut, deleteAccount } = useSession();

  const [biometric, setBiometric] = useState(false);
  const [available, setAvailable] = useState(false);

  useEffect(() => {
    void (async () => {
      const [hardware, enrolled, saved] = await Promise.all([
        LocalAuthentication.hasHardwareAsync(),
        LocalAuthentication.isEnrolledAsync(),
        AsyncStorage.getItem(BIOMETRIC_KEY),
      ]);

      // Proposé seulement si le téléphone en a ET que quelque chose y est
      // enrôlé : un interrupteur qui ne fait rien est pire qu'absent.
      setAvailable(hardware && enrolled);
      setBiometric(saved === 'true');
    })();
  }, []);

  const toggleBiometric = async (next: boolean) => {
    if (next) {
      // On VÉRIFIE avant d'activer : activer un verrou sans l'avoir essayé une
      // fois, c'est risquer de s'enfermer dehors au prochain lancement.
      const result = await LocalAuthentication.authenticateAsync({
        promptMessage: t('login.biometric'),
      });

      if (!result.success) return;
    }

    setBiometric(next);
    await AsyncStorage.setItem(BIOMETRIC_KEY, String(next));
  };

  const confirmDelete = () => {
    Alert.alert(t('profile.deleteAccount'), t('profile.deleteWarning'), [
      { text: t('common.cancel'), style: 'cancel' },
      {
        text: t('profile.deleteAccount'),
        style: 'destructive',
        onPress: () => {
          void (async () => {
            try {
              await deleteAccount();
              /*
               * Le slug n'est oublié que s'il a été CHOISI — C15 bloc 4. En
               * mono-client il vient d'une constante de build : l'effacer ne
               * changerait rien, et renvoyer vers l'écran Société montrerait un
               * formulaire qui n'a plus de raison d'être.
               */
              if (!IS_SINGLE_TENANT) await forgetSlug();
              router.replace(IS_SINGLE_TENANT ? '/(auth)/login' : '/(auth)/company');
            } catch (error) {
              Alert.alert(t('profile.deleteAccount'), describeActionError(error, t));
            }
          })();
        },
      },
    ]);
  };

  return (
    <SafeAreaView className="flex-1 bg-background" edges={['top']}>
      <ScrollView
        keyboardShouldPersistTaps="handled"
        contentContainerClassName="gap-3 px-5 pb-8 pt-4"
      >
        <Text className="text-[22px] font-bold text-foreground">{t('profile.title')}</Text>

        {profile ? (
          <Card className="gap-1">
            <Text className="text-[18px] font-semibold text-foreground">
              {profile.firstName} {profile.lastName}
            </Text>
            <Text className="text-[15px] text-muted-foreground">
              {subtitle ?? profile.email ?? profile.username}
            </Text>
          </Card>
        ) : null}

        {/*
          LES RÉGLAGES — langue, thème, notifications.

          Ils vivaient ici ; la maquette 13 d'Anass en fait un écran à part
          (L2 bloc 8). Le composant est donc PARTAGÉ, et le profil le rend
          encore pour les espaces qui n'ont pas d'écran Paramètres. Le livreur
          passe `settings={false}` : les avoir aux deux endroits ferait deux
          interrupteurs « mode sombre » écrivant la même préférence.
        */}
        {settings ? <SettingsControls /> : null}

        {/* --- Mot de passe — C12 bloc 2c, les trois espaces d'un coup --- */}
        <PasswordCard />

        {/* --- Biométrie --- */}
        {available ? (
          <Card className="flex-row items-center justify-between gap-3">
            <View className="flex-1 flex-row items-center gap-2">
              <Fingerprint size={20} className="text-muted-foreground" />
              <Text className="text-[16px] text-foreground">{t('profile.biometric')}</Text>
            </View>
            <Switch value={biometric} onValueChange={(next) => void toggleBiometric(next)} />
          </Card>
        ) : null}

        {/*
          « À propos », le canal, le runtime et l'identifiant du bundle —
          C14 bloc 4. La version y figure : la carte la porte déjà.
        */}
        <UpdateCard />

        {/* --- Pages légales : exigées par les deux magasins --- */}
        <Card className="gap-1">
          <LegalLink label={t('profile.privacy')} url={LEGAL.privacy} />
          <LegalLink label={t('profile.terms')} url={LEGAL.terms} />
        </Card>

        <Button
          label={t('profile.logout')}
          icon={LogOut}
          tone="ghost"
          onPress={() => void signOut()}
          full
        />

        <Button
          label={t('profile.deleteAccount')}
          icon={Trash2}
          tone="danger"
          onPress={confirmDelete}
          full
        />

        <View className="flex-row items-start gap-2 px-1">
          <ShieldAlert size={16} className="text-muted-foreground" />
          <Text className="flex-1 text-[13px] text-muted-foreground">
            {t('profile.deleteWarning')}
          </Text>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}

function LegalLink({ label, url }: { label: string; url: string }) {
  return (
    <Pressable
      accessibilityRole="link"
      className="min-h-11 justify-center"
      onPress={() => void Linking.openURL(url)}
    >
      <Text className="text-[16px] text-primary">{label}</Text>
    </Pressable>
  );
}
