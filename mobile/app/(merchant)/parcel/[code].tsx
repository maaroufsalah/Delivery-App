import { STATUS_GROUP_COLORS, getParcelStatus, isEditableBySeller } from '@delivery/shared';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { ArrowLeft, MessageSquareWarning, Pencil, Trash2 } from 'lucide-react-native';
import { Alert, Pressable, ScrollView, Text, View } from 'react-native';
import { useGoBack } from '@/lib/go-back';
import { useI18n } from '@/i18n';
import { describeActionError } from '@/lib/use-action';
import { useDeleteParcel, useParcel, useTracking } from '@/lib/merchant';
import { formatStamp } from '@/lib/datetime';
import { Button, Card, Screen, SkeletonCard, StatusBadge, Timeline } from '@/ui';

/**
 * DÉTAIL D'UN COLIS, vu par son VENDEUR — M2.
 *
 * **La timeline vient du suivi PUBLIC, pas du détail.** Le détail porte les
 * événements complets : statut exact, commentaire de l'agent, son nom. Le suivi
 * public replie les trente-quatre statuts en sept familles, retire commentaires
 * et auteurs, et fond les relances consécutives en une seule ligne.
 *
 * C'est cette version-là qu'on montre, et la mission le demandait nommément.
 * L'exemple qui justifie la règle : « 3e tentative — le client dit qu'il n'a
 * jamais commandé ça » est une note de travail entre l'agence et son livreur.
 * Le vendeur a besoin de savoir que le colis en est aux relances ; pas de lire
 * ce qu'on dit de son acheteur.
 *
 * Le reste — destinataire, destination, COD, frais — lui appartient : c'est sa
 * marchandise et son argent. La trésorerie interne, elle, ne descend pas
 * (D-060), et les notes d'agence non plus : l'API les retire pour lui, ce qui
 * vaut mieux que de compter sur cet écran pour ne pas les afficher.
 */
export default function MerchantParcelScreen() {
  const { t, status, locale } = useI18n();
  const router = useRouter();
  // Une notification ouvre cette fiche sans rien derrière : on se rabat.
  const goBack = useGoBack('/(merchant)/parcels');
  const { code } = useLocalSearchParams<{ code: string }>();

  const parcel = useParcel(code);
  const tracking = useTracking(code);
  const remove = useDeleteParcel(code ?? '');

  // Confirmation NATIVE : une suppression ne part pas sur un seul toucher.
  const confirmDelete = () =>
    Alert.alert(t('sellerEdit.deleteTitle', { code: code ?? '' }), t('sellerEdit.deleteHint'), [
      { text: t('common.cancel'), style: 'cancel' },
      {
        text: t('sellerEdit.delete'),
        style: 'destructive',
        onPress: () =>
          remove.mutate(undefined, {
            onSuccess: () => router.replace('/(merchant)/parcels'),
            onError: (error) => Alert.alert(describeActionError(error, t)),
          }),
      },
    ]);

  const definition = parcel.data ? getParcelStatus(parcel.data.statusId) : null;

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
        <Text className="flex-1 font-mono text-[17px] text-foreground">{code}</Text>
        {definition ? (
          <StatusBadge label={status(definition.key)} color={definition.color} />
        ) : null}
      </View>

      {parcel.isLoading ? (
        <View className="gap-3 px-5 pt-2">
          <SkeletonCard />
          <SkeletonCard />
        </View>
      ) : (
        <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-3 px-5 pb-8">
          {/* --- Ce qui lui appartient --- */}
          <Card className="gap-2">
            <Row label={t('merchant.recipient')} value={parcel.data?.recipientName ?? '—'} />
            <Row
              label={t('merchant.destination')}
              value={[parcel.data?.destinationCityName, parcel.data?.destinationDistrictName]
                .filter(Boolean)
                .join(' · ')}
            />
            <View className="border-t border-border pt-2">
              <Row label={t('merchant.cod')} value={money(parcel.data?.codAmount ?? 0)} strong />
              <Row label={t('merchant.fee')} value={money(parcel.data?.deliveryFee ?? 0)} />
              {/* Recette 3 point 3 : le paiement attendu d'un colis Pro. */}
              {parcel.data?.expectedPayment ? (
                <Row
                  label={t('parcel.expectedPayment')}
                  value={t(`expectedPayment.${parcel.data.expectedPayment}`)}
                />
              ) : null}
            </View>
          </Card>

          {/*
            MODIFIER / SUPPRIMER — recette 3, point 3. Tant que le colis n'est pas
            ramassé ; le serveur rend 422 après, et l'écran ne propose plus rien.
          */}
          {parcel.data &&
          parcel.data.kind === 'parcel' &&
          isEditableBySeller(parcel.data.statusId) ? (
            <View className="flex-row gap-2">
              <View className="flex-1">
                <Button
                  label={t('sellerEdit.edit')}
                  icon={Pencil}
                  tone="soft"
                  onPress={() =>
                    router.push(`/(merchant)/parcel-edit?code=${encodeURIComponent(code ?? '')}`)
                  }
                  full
                />
              </View>
              <View className="flex-1">
                <Button
                  label={t('sellerEdit.delete')}
                  icon={Trash2}
                  tone="danger"
                  busy={remove.isPending}
                  onPress={confirmDelete}
                  full
                />
              </View>
            </View>
          ) : null}

          {/* --- Le suivi, dans sa version publique --- */}
          <Text className="pt-2 text-[18px] font-semibold text-foreground">
            {t('merchant.timeline')}
          </Text>

          {tracking.isLoading ? (
            <SkeletonCard />
          ) : (tracking.data?.steps ?? []).length === 0 ? (
            <Text className="px-1 text-[15px] text-muted-foreground">{t('merchant.noSteps')}</Text>
          ) : (
            <Card>
              {/*
                LE composant de timeline, et non plus une copie locale — M5.

                Le vendeur voit le suivi PUBLIC : des familles d'étapes, pas les
                trente-huit statuts internes (D-060, la même aveuglement que sur
                la trésorerie). Ce qui change ici est le rendu, pas le contenu —
                et il devient celui du livreur et de l'agence.

                La couleur vient du référentiel de la FAMILLE ; l'ordre est
                inversé parce que le suivi public va du plus ancien au plus
                récent, et que la timeline montre l'état courant en tête.
              */}
              <Timeline
                entries={[...(tracking.data?.steps ?? [])].reverse().map((step) => ({
                  id: `${step.group}-${step.at}`,
                  label: t(`statusGroup.${step.group}`),
                  color: STATUS_GROUP_COLORS[step.group] ?? '#4B4B4B',
                  at: formatStamp(step.at, locale),
                  comment: step.cityName,
                }))}
              />
            </Card>
          )}

          <Button
            label={t('merchant.reportProblem')}
            icon={MessageSquareWarning}
            tone="ghost"
            onPress={() =>
              router.push(`/(merchant)/claim/new?parcelCode=${encodeURIComponent(code ?? '')}`)
            }
            full
          />
        </ScrollView>
      )}
    </Screen>
  );
}

function Row({ label, value, strong }: { label: string; value: string; strong?: boolean }) {
  return (
    <View className="flex-row items-baseline justify-between gap-3 py-0.5">
      <Text className="text-[15px] text-muted-foreground">{label}</Text>
      <Text
        className={`flex-1 text-end ${
          strong ? 'text-[17px] font-bold tabular-nums' : 'text-[15px]'
        } text-foreground`}
      >
        {value}
      </Text>
    </View>
  );
}

/** Les montants voyagent en CENTIMES : aucun flottant n'entre dans le calcul. */
function money(cents: number): string {
  return `${(cents / 100).toFixed(2)} MAD`;
}
