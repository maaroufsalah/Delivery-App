import { useLocalSearchParams } from 'expo-router';
import { useFreshKey } from '@/lib/use-fresh-key';
import { ArrowLeft, Send } from 'lucide-react-native';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, Text, View } from 'react-native';
import { useGoBack } from '@/lib/go-back';
import { useI18n } from '@/i18n';
import { useClaim, useReplyClaim } from '@/lib/claims';
import { formatStamp } from '@/lib/datetime';
import { Button, Card, Field, Screen, SkeletonCard } from '@/ui';

/**
 * LE FIL d'une réclamation — M2 bloc 6.
 *
 * **L'écran ne filtre rien.** `ClaimsService` retire déjà les entrées internes
 * du fil d'un e-commerçant, et refuse le téléchargement des pièces jointes qui
 * y sont attachées. Compter sur l'écran pour cacher ce que l'API envoie serait
 * du théâtre : la donnée serait déjà sur l'appareil.
 *
 * **Le plus ancien en haut.** Un fil de discussion se lit dans l'ordre où il
 * s'est écrit ; l'inverser obligerait à remonter pour comprendre une réponse.
 */
function MerchantClaimScreenContent() {
  const { t, locale } = useI18n();
  // Une notification ouvre cette fiche sans rien derrière : on se rabat.
  const goBack = useGoBack('/(merchant)/claims');
  const { code } = useLocalSearchParams<{ code: string }>();

  const claim = useClaim(code);
  const reply = useReplyClaim(code ?? '');

  const [message, setMessage] = useState('');

  const send = () => {
    reply.mutate(message.trim(), {
      onSuccess: () => setMessage(''),
    });
  };

  return (
    <Screen>
      <View className="flex-row items-center gap-3 px-5 py-3">
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('common.close')}
          className="min-h-11 min-w-11 items-center justify-center"
          hitSlop={8}
          onPress={goBack}
        >
          <ArrowLeft size={24} className="text-foreground" />
        </Pressable>
        <Text className="flex-1 text-[18px] font-semibold text-foreground" numberOfLines={1}>
          {claim.data?.subject ?? code}
        </Text>
      </View>

      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        className="flex-1"
      >
        {claim.isLoading ? (
          <View className="gap-2 px-5">
            <SkeletonCard />
            <SkeletonCard />
          </View>
        ) : (
          <ScrollView
            keyboardShouldPersistTaps="handled"
            contentContainerClassName="gap-3 px-5 pb-4"
          >
            {/* Le message d'ouverture, puis le fil. */}
            <Card className="gap-1">
              <Text className="text-[14px] text-muted-foreground">{claim.data?.typeLabel}</Text>
              <Text className="text-[15px] text-foreground">{claim.data?.message}</Text>
            </Card>

            {(claim.data?.events ?? []).map((event) => (
              <Card key={event.id} className="gap-1">
                <Text className="text-[13px] text-muted-foreground">
                  {event.authorName} · {formatStamp(event.createdAt, locale)}
                </Text>
                {event.body ? (
                  <Text className="text-[15px] text-foreground">{event.body}</Text>
                ) : null}
              </Card>
            ))}
          </ScrollView>
        )}

        <View className="gap-2 px-5 pb-4">
          <Field label={t('claims.message')} value={message} onChangeText={setMessage} multiline />
          <Button
            label={t('claims.reply')}
            icon={Send}
            onPress={send}
            disabled={message.trim().length === 0}
            busy={reply.isPending}
            full
          />
        </View>
      </KeyboardAvoidingView>
    </Screen>
  );
}

/** Coque Recette 7 (point 5) : le contenu repart vide à chaque `code` et à chaque fermeture — `useFreshKey`. */
export default function MerchantClaimScreen() {
  const { code } = useLocalSearchParams<{ code: string }>();
  return <MerchantClaimScreenContent key={useFreshKey(code)} />;
}
