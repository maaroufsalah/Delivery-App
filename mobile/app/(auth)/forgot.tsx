import type { ForgotPasswordResult } from '@delivery/shared';
import { useRouter } from 'expo-router';
import { ArrowLeft, Phone } from 'lucide-react-native';
import { useState } from 'react';
import { KeyboardAvoidingView, Linking, Platform, Pressable, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useI18n } from '@/i18n';
import { forgotPassword } from '@/lib/account';
import { Button, Card, Field } from '@/components/ui';

/**
 * « MOT DE PASSE OUBLIÉ » — C12 bloc 2b.
 *
 * **L'écran dit la vérité : personne ne recevra d'e-mail.** Tant que B-13
 * n'est pas livré, la seule issue est d'appeler son agence — et le numéro
 * s'affiche, en gros, sur un bouton qui compose. C'est un livreur debout dans
 * la rue avec un téléphone en main : lui promettre un message qui ne partira
 * pas lui ferait attendre une heure avant de faire le seul geste utile.
 *
 * **La confirmation est la même que l'adresse existe ou non.** C'est la règle
 * de l'API et l'écran ne la contourne pas : distinguer les deux cas ferait de
 * ce formulaire un annuaire des comptes de la société.
 */
export default function ForgotPasswordScreen() {
  const { t } = useI18n();
  const router = useRouter();

  const [email, setEmail] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<ForgotPasswordResult | null>(null);

  const submit = async () => {
    setBusy(true);
    setError(null);

    try {
      setDone(await forgotPassword(email));
    } catch {
      // Un seul message : la route ne refuse jamais pour une raison métier, un
      // échec ici veut dire que rien n'est parti et qu'il faut réessayer.
      setError(t('forgot.failed'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <SafeAreaView className="flex-1 bg-background">
      <View className="flex-row items-center gap-3 px-5 py-3">
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('common.close')}
          className="min-h-11 min-w-11 items-center justify-center"
          hitSlop={8}
          onPress={() => router.back()}
        >
          <ArrowLeft size={24} className="text-foreground" />
        </Pressable>
        <Text className="text-[20px] font-bold text-foreground">{t('forgot.title')}</Text>
      </View>

      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        className="flex-1 justify-between px-6 pb-8"
      >
        {done ? (
          <View className="gap-4 pt-6">
            <Text className="text-[16px] text-foreground">{t('forgot.done')}</Text>

            {done.supportPhone ? (
              <Pressable
                accessibilityRole="button"
                onPress={() => void Linking.openURL(`tel:${done.supportPhone ?? ''}`)}
              >
                <Card className="flex-row items-center gap-3">
                  <Phone size={22} className="text-foreground" />
                  <View className="flex-1">
                    <Text className="text-[15px] text-muted-foreground">{done.companyName}</Text>
                    <Text className="text-[20px] font-bold tabular-nums text-foreground">
                      {done.supportPhone}
                    </Text>
                  </View>
                </Card>
              </Pressable>
            ) : (
              <Text className="text-[15px] text-muted-foreground">{t('forgot.noPhone')}</Text>
            )}

            <Text className="text-[14px] text-muted-foreground">
              {done.emailEnabled ? t('forgot.mailSent') : t('forgot.mailPending')}
            </Text>
          </View>
        ) : (
          <View className="gap-5 pt-6">
            <Text className="text-[16px] text-muted-foreground">{t('forgot.hint')}</Text>

            <Field
              label={t('login.email')}
              value={email}
              onChangeText={setEmail}
              autoCapitalize="none"
              autoCorrect={false}
              autoComplete="email"
              inputMode="email"
              onSubmitEditing={() => void submit()}
            />

            {error ? <Text className="text-[15px] text-destructive">{error}</Text> : null}
          </View>
        )}

        {done ? (
          <Button label={t('forgot.backToLogin')} onPress={() => router.back()} full />
        ) : (
          <Button
            label={t('forgot.submit')}
            onPress={() => void submit()}
            busy={busy}
            disabled={email.trim() === ''}
            full
          />
        )}
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
