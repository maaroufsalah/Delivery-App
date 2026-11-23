import { Building2 } from 'lucide-react-native';
import { Redirect, useRouter } from 'expo-router';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useI18n } from '@/i18n';
import { isValidSlug, saveSlug } from '@/lib/tenant';
import { IS_SINGLE_TENANT } from '@/config/brand';
import { Button, Field } from '@/components/ui';

/**
 * L'écran SOCIÉTÉ — M1, docs/DECISIONS.md D-075.
 *
 * Le premier écran de la première ouverture, et le seul endroit où l'on demande
 * quelque chose que le livreur ne connaît pas forcément par cœur. D'où le mot
 * « nom court » plutôt que « sous-domaine » ou « slug » : c'est ce que son
 * agence lui dira au téléphone.
 *
 * La validation est faite AVANT d'enregistrer : le slug entre dans une URL, et
 * un slug bricolé ne doit pas pouvoir désigner un autre hôte que celui qu'on
 * croit appeler.
 *
 * **L'écran est CONSERVÉ mais inatteignable en mono-client — C15 bloc 4.**
 * Plus rien n'y navigue ; il se renvoie lui-même vers la connexion si un lien
 * profond ou un historique l'y ramène. Le supprimer aurait voulu dire le
 * réécrire le jour où un deuxième transporteur arrive, alors qu'il suffit de
 * remettre `BRAND_TENANT` à `null` (`config/brand.ts`).
 */
export default function CompanyScreen() {
  const { t } = useI18n();
  const router = useRouter();

  const [slug, setSlug] = useState('');
  const [error, setError] = useState<string | null>(null);

  // APRÈS les crochets, jamais avant : un retour anticipé changerait l'ordre
  // des appels d'un rendu à l'autre, ce que React interdit.
  if (IS_SINGLE_TENANT) return <Redirect href="/(auth)/login" />;

  const submit = async () => {
    if (!isValidSlug(slug)) {
      setError(t('tenant.invalid'));
      return;
    }

    await saveSlug(slug);
    router.replace('/(auth)/login');
  };

  return (
    <SafeAreaView className="flex-1 bg-background">
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        className="flex-1 justify-between px-6 py-8"
      >
        <View className="gap-6 pt-12">
          <Building2 size={40} className="text-primary" />

          <View className="gap-2">
            <Text className="text-[28px] font-bold text-foreground">{t('tenant.title')}</Text>
            <Text className="text-[16px] text-muted-foreground">{t('tenant.hint')}</Text>
          </View>

          <Field
            label={t('tenant.title')}
            value={slug}
            onChangeText={(value) => {
              setSlug(value);
              setError(null);
            }}
            placeholder={t('tenant.placeholder')}
            autoCapitalize="none"
            autoCorrect={false}
            autoComplete="off"
            inputMode="url"
            onSubmitEditing={() => void submit()}
          />

          {error ? <Text className="text-[15px] text-destructive">{error}</Text> : null}
        </View>

        {/* En BAS : c'est là que le pouce arrive sur un téléphone tenu d'une main. */}
        <Button label={t('tenant.continue')} onPress={() => void submit()} full />
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
