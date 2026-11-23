import { RefreshCw } from 'lucide-react-native';
import { Pressable, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useI18n } from '@/i18n';
import { useAppUpdate } from '@/lib/updates';

/**
 * « MISE À JOUR PRÊTE — REDÉMARRER » — C14 bloc 4.
 *
 * **Un bandeau, pas une boîte de dialogue, et surtout pas un rechargement.**
 * `reloadAsync()` relance l'application sur-le-champ : l'appeler tout seul
 * couperait un livreur en train de saisir un montant encaissé devant une
 * porte, et lui ferait retaper. Le bandeau attend ; c'est lui qui choisit.
 *
 * **En HAUT, sous la zone sûre.** Le bas appartient à la barre d'onglets et
 * au bandeau hors ligne (D-077) : deux bandes empilées au-dessus du pouce
 * cacheraient ce qu'on vient toucher.
 *
 * **Il ne se ferme pas.** Un bandeau qu'on écarte revient au prochain
 * lancement et devient du bruit ; celui-ci disparaît quand on redémarre,
 * c'est-à-dire quand il a servi. Il ne coûte que sa hauteur, et seulement
 * après un téléchargement réussi.
 */
export function UpdateBanner() {
  const { t } = useI18n();
  const insets = useSafeAreaInsets();
  const { status, restart } = useAppUpdate();

  if (status !== 'ready') return null;

  return (
    <View
      className="flex-row items-center gap-3 bg-primary px-4 pb-2.5"
      style={{ paddingTop: insets.top + 10 }}
      accessibilityRole="alert"
    >
      <RefreshCw size={18} color="#FFFFFF" />

      <Text className="flex-1 text-[15px] font-medium text-white" numberOfLines={2}>
        {t('update.ready')}
      </Text>

      <Pressable
        accessibilityRole="button"
        accessibilityLabel={t('update.restart')}
        onPress={() => void restart()}
        className="min-h-11 items-center justify-center rounded-xl bg-white/20 px-3.5"
        style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
      >
        <Text className="text-[15px] font-semibold text-white">{t('update.restart')}</Text>
      </Pressable>
    </View>
  );
}
