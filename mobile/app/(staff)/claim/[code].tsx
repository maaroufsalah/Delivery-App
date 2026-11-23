import { useLocalSearchParams } from 'expo-router';
import { useFreshKey } from '@/lib/use-fresh-key';
import { ArrowLeft, Lock, Send } from 'lucide-react-native';
import { useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  Switch,
  Text,
  View,
} from 'react-native';
import { useGoBack } from '@/lib/go-back';
import { useI18n } from '@/i18n';
import { can, useSession } from '@/lib/session';
import { useReplyStaffClaim, useStaffClaim } from '@/lib/staff';
import { formatStamp } from '@/lib/datetime';
import { Button, Card, Field, Screen, SkeletonCard } from '@/ui';

/**
 * TRAITER UNE RÉCLAMATION — M3 bloc 8.
 *
 * **Répondre et clôturer en un seul geste.** `replyClaimSchema` accepte les
 * deux à la fois, et c'est le cas le plus courant : on répond EN clôturant.
 * L'imposer en deux appels ferait deux lignes d'historique pour un seul acte.
 *
 * **La note interne est ici, et seulement ici.** Elle ne descend pas chez
 * l'e-commerçant — `ClaimsService` la filtre de son fil et refuse ses pièces
 * jointes. L'interrupteur DIT ce qu'il fait, parce qu'une note écrite en
 * croyant qu'elle est privée est le pire des malentendus.
 *
 * Les transitions viennent du SERVEUR (`allowedTransitions` du détail), pas
 * d'une liste écrite ici : le statut suivant dépend de l'état courant, et le
 * back-office dit déjà lesquels sont possibles.
 */
function StaffClaimScreenContent() {
  const { t, locale } = useI18n();
  // Une notification ouvre cette fiche sans rien derrière : on se rabat.
  const goBack = useGoBack('/(staff)/claims');
  const { profile } = useSession();
  const { code } = useLocalSearchParams<{ code: string }>();

  const claim = useStaffClaim(code);
  const reply = useReplyStaffClaim(code ?? '');

  const [message, setMessage] = useState('');
  const [internal, setInternal] = useState(false);

  const mayHandle = can(profile, 'claims.handle');
  const closable = (claim.data?.allowedTransitions ?? []).find(
    (target) => target === 'resolved' || target === 'rejected',
  );

  const send = (status?: string) => {
    reply.mutate(
      { body: message.trim(), isInternal: internal, status },
      { onSuccess: () => setMessage('') },
    );
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
            <Card className="gap-1">
              <Text className="text-[14px] text-muted-foreground">
                {claim.data?.clientName} · {claim.data?.typeLabel}
              </Text>
              <Text className="text-[15px] text-foreground">{claim.data?.message}</Text>
            </Card>

            {(claim.data?.events ?? []).map((event) => (
              <Card
                key={event.id}
                className={`gap-1 ${event.isInternal ? 'border border-warning' : ''}`}
              >
                <View className="flex-row items-center gap-2">
                  {event.isInternal ? <Lock size={14} className="text-warning" /> : null}
                  <Text className="flex-1 text-[13px] text-muted-foreground">
                    {event.authorName} · {formatStamp(event.createdAt, locale)}
                  </Text>
                </View>
                {event.body ? (
                  <Text className="text-[15px] text-foreground">{event.body}</Text>
                ) : null}
              </Card>
            ))}
          </ScrollView>
        )}

        <View className="gap-2 px-5 pb-4">
          <Field label={t('claims.message')} value={message} onChangeText={setMessage} multiline />

          <Card className="flex-row items-center justify-between gap-3">
            <View className="flex-1">
              <Text className="text-[16px] text-foreground">{t('staff.internalNote')}</Text>
              <Text className="text-[13px] text-muted-foreground">{t('staff.internalHint')}</Text>
            </View>
            <Switch value={internal} onValueChange={setInternal} />
          </Card>

          <Button
            label={t('claims.reply')}
            icon={Send}
            onPress={() => send()}
            disabled={message.trim().length === 0}
            busy={reply.isPending}
            full
          />

          {mayHandle && closable ? (
            <Button
              label={t('staff.close')}
              tone="ghost"
              onPress={() => send(closable)}
              disabled={message.trim().length === 0}
              busy={reply.isPending}
              full
            />
          ) : null}
        </View>
      </KeyboardAvoidingView>
    </Screen>
  );
}

/** Coque Recette 7 (point 5) : le contenu repart vide à chaque `code` et à chaque fermeture — `useFreshKey`. */
export default function StaffClaimScreen() {
  const { code } = useLocalSearchParams<{ code: string }>();
  return <StaffClaimScreenContent key={useFreshKey(code)} />;
}
