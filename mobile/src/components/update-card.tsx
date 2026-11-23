import Constants from 'expo-constants';
import { Info, RefreshCw } from 'lucide-react-native';
import { Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { useAppUpdate } from '@/lib/updates';
import { useTenantTheme } from '@/theme/tenant-theme';
import { Button, Card, Divider } from '@/ui';

/**
 * « À PROPOS », ET CE QUI PROUVE QUEL BUNDLE TOURNE — C14 bloc 4.
 *
 * **La ligne qui manquait au diagnostic.** Pendant tout le chantier L2, on ne
 * pouvait pas distinguer « l'OTA a été téléchargée mais pas appliquée » de
 * « la requête n'est jamais partie » : rien, dans l'application, ne disait
 * lequel des deux. Cette carte le dit en une ligne.
 *
 * `isEmbeddedLaunch` tranche : **« bundle APK »** veut dire que le code qui
 * tourne est celui compilé dans le binaire, donc qu'aucune mise à jour n'a été
 * appliquée. Un identifiant à la place veut dire l'inverse, et on peut le
 * comparer à l'update group publié.
 *
 * **Le canal et le runtime sont là pour la même raison.** Un APK sur le canal
 * `production` ne verra jamais une mise à jour publiée sur `preview`, et un
 * runtime `1.0.1` ne verra jamais celles de `1.0.0` — ce sont les deux
 * premières choses qu'on vérifie, et les deux qu'on ne pouvait pas lire.
 */
export function UpdateCard() {
  const { t } = useI18n();
  const { brand } = useTenantTheme();
  const { status, info, check } = useAppUpdate();

  const busy = status === 'checking' || status === 'downloading';

  return (
    <Card className="gap-1 p-1">
      <View className="flex-row items-center gap-3 px-3 py-3">
        <Info size={20} className="text-muted-foreground" />
        <View className="flex-1 gap-0.5">
          <Text className="text-[16px] text-foreground">{t('profile.about')}</Text>
          <Text className="text-[14px] text-muted-foreground">
            {brand?.name ?? ''} ·{' '}
            {t('profile.version', { version: Constants.expoConfig?.version ?? '—' })}
          </Text>
        </View>
      </View>

      <Divider />

      <View className="gap-1.5 px-3 py-3">
        <Row label={t('update.channel')} value={info.channel ?? '—'} />
        <Row label={t('update.runtime')} value={info.runtimeVersion ?? '—'} />
        {/*
          LA LIGNE QUI PROUVE. « bundle APK » = aucune OTA appliquée ; un
          identifiant = celle-là tourne, et il se compare à l'update group.
        */}
        <Row
          label={t('update.bundle')}
          value={info.embedded ? t('update.embedded') : (info.updateId ?? '—')}
          mono={!info.embedded}
        />
      </View>

      <View className="px-3 pb-3">
        <Button
          label={t(busy ? 'update.checking' : 'update.check')}
          icon={RefreshCw}
          tone="ghost"
          busy={busy}
          onPress={() => void check()}
          full
        />

        {/*
          Le RÉSULTAT de la vérification manuelle, dit à voix basse. Sans lui,
          un bouton qui ne change rien à l'écran laisse croire qu'il n'a rien
          fait — alors qu'il vient précisément de répondre « vous êtes à jour ».
        */}
        {status === 'current' ? (
          <Text className="pt-2 text-center text-[14px] text-muted-foreground">
            {t('update.current')}
          </Text>
        ) : null}

        {status === 'unavailable' ? (
          <Text className="pt-2 text-center text-[14px] text-muted-foreground">
            {t('update.unavailable')}
          </Text>
        ) : null}
      </View>
    </Card>
  );
}

/** Une ligne étiquette / valeur, la valeur en monospace quand c'est un identifiant. */
function Row({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <View className="flex-row items-start justify-between gap-3">
      <Text className="text-[14px] text-muted-foreground">{label}</Text>
      <Text
        className={`flex-1 text-end text-[14px] text-foreground ${mono ? 'font-mono text-[12px]' : ''}`}
        numberOfLines={2}
      >
        {value}
      </Text>
    </View>
  );
}
