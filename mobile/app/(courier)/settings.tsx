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
 * LES PARAMÈTRES — L2 bloc 8, maquette 13.
 *
 * **Ce que l'écran gagne à exister.** Les réglages vivaient au milieu du
 * profil, entre la carte d'identité et la suppression de compte. Un livreur qui
 * cherchait le mode sombre faisait défiler huit cartes ; maintenant il ouvre
 * Plus > Paramètres.
 *
 * **Les contrôles ne sont pas recopiés** : `SettingsControls` est le même
 * composant que le profil rend pour les espaces marchand et agence, qui n'ont
 * pas d'écran Paramètres. Deux interrupteurs « mode sombre » écrivant la même
 * préférence auraient fini par se désaccorder.
 *
 * **Ce que la maquette montre et qui n'est PAS ici** : le « son des
 * notifications ». Un interrupteur qui ne coupe rien est pire qu'absent, et le
 * son d'une notification push se règle dans les réglages du système tant que
 * les canaux Android ne sont pas posés — c'est B-18.
 */
export default function CourierSettings() {
  const { t } = useI18n();
  const { signOut } = useSession();

  return (
    <Screen>
      <ScreenHeader title={t('settings.title')} />

      <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-3 px-5 pb-8">
        <SettingsControls />

        {/*
          « À PROPOS », et ce qui prouve quel bundle tourne — C14 bloc 4.

          La carte est PARTAGÉE : le profil des trois espaces la rend aussi,
          parce qu'un agent d'agence au téléphone avec un livreur a besoin de
          lire le même identifiant que lui.
        */}
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
