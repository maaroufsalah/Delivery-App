import { ChevronRight, LogOut } from 'lucide-react-native';
import { Linking, Pressable, ScrollView, Text } from 'react-native';
import { useI18n } from '@/i18n';
import { useSession } from '@/lib/session';
import { SettingsControls } from '@/components/settings-controls';
import { UpdateCard } from '@/components/update-card';
import { Button, Card, Divider, Screen, ScreenHeader } from '@/ui';

/** Les deux pages légales, exigées par les magasins, servies par la plateforme. */
const LEGAL = {
  privacy: 'https://delivery.nocido.com/privacy',
  terms: 'https://delivery.nocido.com/terms',
};

/**
 * LES PARAMÈTRES DU VENDEUR — V2 vendeur bloc 1, spécification §12.
 *
 * Le pendant exact de l'écran du livreur (L2 bloc 8), et pour la même raison :
 * les réglages vivaient au milieu du profil, entre la carte d'identité et la
 * suppression de compte. « Plus > Paramètres » est l'endroit où on les cherche.
 *
 * **Rien n'est recopié.** `SettingsControls` et `UpdateCard` sont les mêmes
 * composants que chez le livreur : deux interrupteurs « mode sombre » écrivant
 * la même préférence auraient fini par se désaccorder, et deux cartes « À
 * propos » auraient affiché deux identifiants de bundle différents au même
 * appel téléphonique.
 */
export default function MerchantSettings() {
  const { t } = useI18n();
  const { signOut } = useSession();

  return (
    <Screen>
      <ScreenHeader title={t('settings.title')} />

      <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-3 px-5 pb-8">
        <SettingsControls />
        <UpdateCard />

        <Card className="gap-1 p-1">
          <LegalRow label={t('profile.privacy')} url={LEGAL.privacy} />
          <Divider />
          <LegalRow label={t('profile.terms')} url={LEGAL.terms} />
        </Card>

        <Button
          label={t('profile.logout')}
          icon={LogOut}
          tone="ghost"
          onPress={() => void signOut()}
          full
        />
      </ScrollView>
    </Screen>
  );
}

function LegalRow({ label, url }: { label: string; url: string }) {
  return (
    <Pressable
      accessibilityRole="link"
      onPress={() => void Linking.openURL(url)}
      className="min-h-[52px] flex-row items-center gap-3 rounded-2xl px-3"
      style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
    >
      <Text className="flex-1 text-[16px] text-foreground">{label}</Text>
      <ChevronRight size={18} className="flip-rtl text-muted-foreground" />
    </Pressable>
  );
}
