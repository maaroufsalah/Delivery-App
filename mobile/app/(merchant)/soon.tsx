import { Clock } from 'lucide-react-native';
import { View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useI18n } from '@/i18n';
import { EmptyState } from '@/components/ui';

/**
 * « Bientôt » — M1, réduit en M2.
 *
 * L'espace du marchand est ouvert : accueil, colis, profil. Cet écran ne sert
 * plus que de destination aux gestes des blocs 5 et 6 qui n'existent pas encore
 * — créer un colis, demander un ramassage, ouvrir une réclamation. Il
 * disparaîtra avec eux.
 */
export default function SoonScreen() {
  const { t } = useI18n();

  return (
    <SafeAreaView className="flex-1 bg-background">
      <View className="flex-1">
        <EmptyState icon={Clock} title={t('app.soon')} hint={t('app.soonHint')} />
      </View>
    </SafeAreaView>
  );
}
