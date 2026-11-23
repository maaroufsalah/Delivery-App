import { I18N_LOCALES, type I18nLocale } from '@delivery/shared';
import { Bell, Globe, Lock, Moon, Sun, Volume2 } from 'lucide-react-native';
import { Alert, Pressable, Switch, Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { useNotificationSettings, useSetNotificationPreference } from '@/lib/notifications';
import { useScanSounds } from '@/lib/scan-feedback';
import { useTenantTheme } from '@/theme/tenant-theme';
import { Card } from '@/ui';

/**
 * LES RÉGLAGES — langue, thème, notifications — L2 bloc 8.
 *
 * **Extraits de `ProfileScreen`, pas recopiés.** La maquette 13 d'Anass fait
 * des Paramètres un écran à part, là où l'application les posait au milieu du
 * profil. Les recopier aurait fait deux interrupteurs « mode sombre » écrivant
 * la même préférence : l'un des deux aurait fini par ne plus la relire.
 *
 * Le profil garde ce qui relève de l'IDENTITÉ et de la sécurité — mot de
 * passe, biométrie, pages légales, suppression de compte — et rend ce bloc
 * lui aussi pour les espaces qui n'ont pas d'écran Paramètres (marchand,
 * agence). Le livreur, lui, le trouve sous Paramètres et nulle part ailleurs.
 */
export function SettingsControls() {
  const { t, locale, setLocale } = useI18n();
  const { preference, setPreference: setThemePreference } = useTenantTheme();

  const notifications = useNotificationSettings();
  const setNotification = useSetNotificationPreference();
  const scanSounds = useScanSounds();

  return (
    <>
      {/* --- Langue --- */}
      <Card className="gap-3">
        <View className="flex-row items-center gap-2">
          <Globe size={20} className="text-muted-foreground" />
          <Text className="text-[16px] font-medium text-foreground">{t('profile.language')}</Text>
        </View>

        <View className="flex-row gap-2">
          {I18N_LOCALES.map((code) => (
            <Pressable
              key={code}
              accessibilityRole="button"
              accessibilityState={{ selected: locale === code }}
              onPress={() => void switchLocale(code, locale, setLocale, t)}
              className={`min-h-11 flex-1 items-center justify-center rounded-xl border ${
                locale === code ? 'border-primary bg-muted' : 'border-border'
              }`}
            >
              <Text className="text-[16px] uppercase text-foreground">{code}</Text>
            </Pressable>
          ))}
        </View>
      </Card>

      {/* --- Thème : Clair par défaut, Sombre à un appui (C15 bloc 5) --- */}
      <Card className="gap-3">
        <View className="flex-row items-center gap-2">
          <Sun size={20} className="text-muted-foreground" />
          <Text className="text-[16px] font-medium text-foreground">{t('profile.theme')}</Text>
        </View>

        <View className="flex-row gap-2">
          {/*
            DEUX CHOIX, PLUS TROIS — C15 bloc 5. « Système » a disparu : un
            livreur ne fait pas le lien entre un réglage Android et une
            application qui devient noire, et voyait donc son thème changer
            sans avoir rien demandé.
          */}
          {(['light', 'dark'] as const).map((value) => (
            <Pressable
              key={value}
              accessibilityRole="button"
              accessibilityState={{ selected: preference === value }}
              onPress={() => setThemePreference(value)}
              className={`min-h-11 flex-1 flex-row items-center justify-center gap-1.5 rounded-xl border ${
                preference === value ? 'border-primary bg-muted' : 'border-border'
              }`}
            >
              {value === 'light' ? (
                <Sun size={16} className="text-foreground" />
              ) : (
                <Moon size={16} className="text-foreground" />
              )}
              <Text className="text-[15px] text-foreground">
                {t(`profile.${value}` as 'profile.light')}
              </Text>
            </Pressable>
          ))}
        </View>
      </Card>

      {/* --- Sons au scan — retours recette 26/09, point 3 --- */}
      <Card className="gap-3">
        <View className="flex-row items-center justify-between gap-3">
          <View className="flex-1 flex-row items-center gap-2">
            <Volume2 size={20} className="text-muted-foreground" />
            <View className="flex-1">
              <Text className="text-[16px] font-medium text-foreground">
                {t('settings.scanSounds')}
              </Text>
              {/* Depuis l'APK 1.2.0 le module audio est lié : le son joue. */}
              <Text className="text-[13px] text-muted-foreground">
                {t('settings.scanSoundsHint')}
              </Text>
            </View>
          </View>
          <Switch
            value={scanSounds.enabled}
            onValueChange={(next) => void scanSounds.setEnabled(next)}
          />
        </View>
      </Card>

      {/* --- Notifications — M2/M3 bloc 9 --- */}
      {(notifications.data?.topics ?? []).length > 0 ? (
        <Card className="gap-3">
          <View className="flex-row items-center gap-2">
            <Bell size={20} className="text-muted-foreground" />
            <Text className="text-[16px] font-medium text-foreground">{t('notif.title')}</Text>
          </View>
          <Text className="text-[13px] text-muted-foreground">{t('notif.hint')}</Text>

          {(notifications.data?.topics ?? []).map((topic) => (
            <View key={topic.key} className="flex-row items-center justify-between gap-3">
              <View className="flex-1">
                <Text className="text-[16px] text-foreground">
                  {t(`notif.topic.${topic.key}` as 'notif.topic.route-assigned')}
                </Text>
                {/*
                  Un sujet CRITIQUE reste visible, et verrouillé. Le cacher
                  laisserait croire qu'on ne le reçoit pas ; l'ouvrir
                  permettrait à un livreur de ne plus voir arriver son travail
                  et de croire l'application cassée (D-081).
                */}
                {topic.critical ? (
                  <View className="flex-row items-center gap-1">
                    <Lock size={12} className="text-muted-foreground" />
                    <Text className="text-[13px] text-muted-foreground">{t('notif.locked')}</Text>
                  </View>
                ) : null}
              </View>

              <Switch
                value={topic.enabled}
                disabled={topic.critical || setNotification.isPending}
                onValueChange={(enabled) => setNotification.mutate({ topic: topic.key, enabled })}
              />
            </View>
          ))}
        </Card>
      ) : null}
    </>
  );
}

/**
 * Changer de langue, et prévenir pour l'arabe.
 *
 * `I18nManager.forceRTL` ne prend effet qu'au prochain lancement : React Native
 * lit le sens d'écriture à l'initialisation des vues natives. Redémarrer
 * l'application de force au milieu d'une tournée serait pire qu'attendre —
 * d'où l'alerte plutôt qu'un `Updates.reloadAsync()`.
 */
async function switchLocale(
  next: I18nLocale,
  current: I18nLocale,
  setLocale: (locale: I18nLocale) => Promise<void>,
  t: (key: 'profile.rtlRestart') => string,
): Promise<void> {
  const wasRtl = current === 'ar';
  await setLocale(next);

  if ((next === 'ar') !== wasRtl) {
    Alert.alert('', t('profile.rtlRestart'));
  }
}
