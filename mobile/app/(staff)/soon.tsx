import { Clock } from 'lucide-react-native';
import { View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useI18n } from '@/i18n';
import { EmptyState } from '@/components/ui';

/**
 * « Bientôt » — M1.
 *
 * L'espace existe dans l'arborescence dès maintenant pour que la phase suivante
 * n'ait qu'à le remplir : les routes, le layout et l'aiguillage par rôle sont
 * déjà en place. Un écran propre vaut mieux qu'une redirection vers un espace
 * qui n'est pas le sien.
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
