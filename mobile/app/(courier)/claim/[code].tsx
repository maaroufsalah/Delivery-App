import { useLocalSearchParams } from 'expo-router';
import { useFreshKey } from '@/lib/use-fresh-key';
import { Send } from 'lucide-react-native';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { useClaim, useReplyClaim } from '@/lib/claims';
import { formatStamp } from '@/lib/datetime';
import { Button, Card, Field, Screen, ScreenHeader, SkeletonCard } from '@/ui';

/**
 * LE FIL D'UNE RÉCLAMATION, CÔTÉ LIVREUR — le complément de L2.
 *
 * **L'écran ne filtre rien.** `ClaimsService` retire déjà les entrées internes
 * du fil de qui ne peut pas traiter (`handler || !event.isInternal`), et refuse
 * le téléchargement des pièces jointes qui y sont attachées. Compter sur
 * l'écran pour cacher ce que l'API envoie serait du théâtre : la donnée serait
 * déjà sur l'appareil.
 *
 * **Il RÉPOND, il ne tranche pas.** Changer le statut, assigner un agent,
 * écrire une note d'agence : le serveur refuse les trois pour lui
 * (`claims.handle`), et l'écran ne les propose donc pas. Il est partie au FAIT
 * — c'est lui qu'on met en cause quand un colis arrive abîmé — pas à la
 * décision.
 *
 * **Le plus ancien en haut.** Un fil se lit dans l'ordre où il s'est écrit ;
 * l'inverser obligerait à remonter pour comprendre une réponse.
 */
function CourierClaimScreenContent() {
  const { t, locale } = useI18n();
  const { code } = useLocalSearchParams<{ code: string }>();

  const claim = useClaim(code);
  const reply = useReplyClaim(code ?? '');

  const [message, setMessage] = useState('');

  // Date et heure à la seconde, comme sur le web (D-254).
  const when = (iso: string) => formatStamp(iso, locale);

  return (
    <Screen>
      <ScreenHeader
        backHref={'/(courier)/claims'}
        title={claim.data?.subject ?? code ?? ''}
        subtitle={claim.data?.code ?? undefined}
      />

      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        className="flex-1"
      >
        {claim.isLoading ? (
          <View className="gap-3 px-5">
            <SkeletonCard />
            <SkeletonCard />
          </View>
        ) : (
          <ScrollView
            keyboardShouldPersistTaps="handled"
            contentContainerClassName="gap-3 px-5 pb-4"
          >
            {/* L'exposé initial, immuable — puis le fil. */}
            <Card className="gap-1">
              <Text className="text-[14px] text-muted-foreground">
                {claim.data?.typeLabel}
                {claim.data?.parcelCode ? ` · ${claim.data.parcelCode}` : ''}
              </Text>
              <Text className="text-[15px] text-foreground">{claim.data?.message}</Text>
            </Card>

            {(claim.data?.events ?? []).map((event) => (
              <Card key={event.id} className="gap-1">
                <Text className="text-[13px] text-muted-foreground">
                  {event.authorName} · {when(event.createdAt)}
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
            onPress={() => reply.mutate(message.trim(), { onSuccess: () => setMessage('') })}
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
export default function CourierClaimScreen() {
  const { code } = useLocalSearchParams<{ code: string }>();
  return <CourierClaimScreenContent key={useFreshKey(code)} />;
}
