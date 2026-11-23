import { getParcelStatus, NOTE_STATUS_COLORS } from '@delivery/shared';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { ArrowLeft } from 'lucide-react-native';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { useGoBack } from '@/lib/go-back';
import { useI18n, type MessageKey } from '@/i18n';
import { useDeliveryNote } from '@/lib/merchant';
import { Card, Divider, Screen, SkeletonCard, StatusBadge } from '@/ui';

/**
 * LE DÉTAIL D'UN BON — V2 vendeur bloc 4, spécification §5.
 *
 * Numéro, date, nombre de colis, liste complète, total COD, statut — la
 * spécification les nomme tous les six, et ils viennent tous de
 * `GET /delivery-notes/:code`, borné par `noteWhere()` depuis C7. Aucune route
 * neuve : un vendeur n'ouvre que ses bons, et la portée le tient sans qu'un
 * écran ait à s'en charger.
 *
 * **Ce que cet écran NE FAIT PAS, et pourquoi.** Pas de téléchargement PDF,
 * pas d'impression. Les deux demandent d'écrire un fichier et de le passer à
 * une autre application — `expo-file-system` et `expo-sharing` — c'est-à-dire
 * deux dépendances NATIVES, donc un nouvel APK. Ce chantier est livré par OTA.
 * Le bon s'imprime depuis le web, où le téléchargement ne coûte rien.
 */
export default function MerchantNoteDetail() {
  const { t } = useI18n();
  const router = useRouter();
  // Une notification ouvre cette fiche sans rien derrière : on se rabat.
  const goBack = useGoBack('/(merchant)/notes');
  const { code } = useLocalSearchParams<{ code: string }>();

  const note = useDeliveryNote(code);
  const data = note.data;

  /*
   * Le total est calculé SUR LES LIGNES rendues, pas demandé au serveur : le
   * bon porte déjà ses colis, et un second appel pour une somme que l'écran a
   * sous les yeux coûterait un aller-retour de 3G d'agence.
   */
  const totalCod = (data?.parcels ?? []).reduce((sum, parcel) => sum + parcel.codAmount, 0);

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
          <ArrowLeft size={24} className="flip-rtl text-foreground" />
        </Pressable>
        <Text className="flex-1 font-mono text-[17px] text-foreground">{code}</Text>
        {data ? (
          <StatusBadge
            label={t(`noteStatus.${data.status}` as MessageKey)}
            color={NOTE_STATUS_COLORS[data.status]}
          />
        ) : null}
      </View>

      {note.isLoading ? (
        <View className="gap-3 px-5">
          <SkeletonCard />
          <SkeletonCard />
        </View>
      ) : !data ? (
        <Text className="px-5 text-[15px] text-muted-foreground">{t('notes.notFound')}</Text>
      ) : (
        <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-3 px-5 pb-8">
          <Card className="gap-1">
            <Text className="text-[15px] text-muted-foreground">
              {t('notes.parcelCount', { count: data.parcelCount })}
            </Text>
            <Text className="text-[28px] font-bold tabular-nums text-foreground">
              {money(totalCod)}
            </Text>
            <Text className="text-[13px] text-muted-foreground">{t('notes.totalCod')}</Text>
          </Card>

          <Card className="gap-1 p-1">
            {data.parcels.map((parcel, index) => {
              const definition = getParcelStatus(parcel.statusId);

              return (
                <View key={parcel.id}>
                  {index > 0 ? <Divider /> : null}
                  <Pressable
                    accessibilityRole="button"
                    onPress={() =>
                      router.push(`/(merchant)/parcel/${encodeURIComponent(parcel.code)}`)
                    }
                    className="min-h-[60px] justify-center gap-0.5 rounded-2xl px-3 py-2"
                    style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
                  >
                    <View className="flex-row items-center justify-between gap-3">
                      <Text className="flex-1 font-mono text-[14px] text-muted-foreground">
                        {parcel.code}
                      </Text>
                      {definition ? (
                        <View
                          className="size-2 rounded-full"
                          style={{ backgroundColor: definition.color }}
                        />
                      ) : null}
                    </View>
                    <Text className="text-[16px] text-foreground" numberOfLines={1}>
                      {parcel.recipientName}
                    </Text>
                    <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
                      {parcel.destinationCityName} · {money(parcel.codAmount)}
                    </Text>
                  </Pressable>
                </View>
              );
            })}
          </Card>
        </ScrollView>
      )}
    </Screen>
  );
}

/** Les montants voyagent en CENTIMES : aucun flottant n'entre dans le calcul. */
function money(cents: number): string {
  return `${(cents / 100).toFixed(2)} MAD`;
}
