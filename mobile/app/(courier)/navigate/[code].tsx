import { findParcelStatus, phonesOf, routeTargetsSender } from '@delivery/shared';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { CircleCheck, MessageCircle, Navigation, Phone } from 'lucide-react-native';
import { Linking, Pressable, Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { useRoutes } from '@/lib/courier';
import { useRefetchOnFocus } from '@/lib/use-refetch-on-focus';
import { useRouteGone } from '@/lib/use-route-gone';
import { routeStopOf, waNumber } from '@/lib/route-stop';
import { Button, Card, EmptyState, Screen, ScreenHeader, StatusBadge } from '@/ui';

/**
 * LIVRAISON EN COURS — L2 bloc 3, maquette 11.
 *
 * L'écran du trajet : le colis qu'on va livrer, qui attend, où, et les trois
 * gestes du guidon — appeler, écrire, ouvrir l'itinéraire. Puis « Arriver sur
 * place », qui n'est pas un statut mais un RACCOURCI : il ouvre la fiche sur sa
 * feuille d'issues, là où le livreur rapporte ce qui s'est passé.
 *
 * **Pourquoi pas de carte.** La maquette en montre une, plein cadre.
 * `react-native-maps` n'est pas lié dans l'APK `ebb98c4b` et l'OTA n'ajoute pas
 * de module natif : la carte serait une image morte. L'itinéraire part donc
 * dans l'application de cartes du téléphone, qui sait suivre la position, dire
 * les virages et que le livreur a déjà choisie. La carte embarquée est au
 * BACKLOG (B-18), et cet écran est sa place.
 *
 * **Aucun appel de plus** : le colis vient de la tournée déjà en cache. Un
 * écran de navigation qui attend le réseau pour afficher une adresse est un
 * écran qu'on regarde à l'arrêt, moteur tournant.
 */
export default function NavigateScreen() {
  const { code } = useLocalSearchParams<{ code: string }>();
  const { t, status } = useI18n();
  const router = useRouter();

  const active = useRoutes('active');

  const found = active.data
    ?.flatMap((route) => route.parcels.map((parcel) => ({ route, parcel })))
    .find((row) => row.parcel.code === code);
  // Relu à chaque retour ; circuit clos par l'agence entre-temps → on revient.
  useRefetchOnFocus(active);
  useRouteGone(!found && active.isFetched && !active.isFetching);

  if (!found) {
    return (
      <Screen>
        <ScreenHeader title={t('circuits.inProgress')} backHref={'/(courier)/circuits'} />
        <EmptyState art="route" title={t('parcel.notFound')} />
      </Screen>
    );
  }

  const { route, parcel } = found;
  const stop = routeStopOf(route.kind, parcel);
  const definition = findParcelStatus(parcel.statusId);
  const toSender = routeTargetsSender(route.kind);

  return (
    <Screen>
      <ScreenHeader
        backHref={'/(courier)/circuits'}
        title={t(toSender ? 'circuits.pickupInProgress' : 'circuits.inProgress')}
        subtitle={parcel.code}
        action={
          definition ? (
            <StatusBadge label={status(definition.key)} color={definition.color} />
          ) : undefined
        }
      />

      <View className="flex-1 justify-end gap-3 px-5 pb-4">
        {/*
          L'ADRESSE EN GRAND, au-dessus du reste.

          C'est ce qu'on relit au feu rouge, casque sur la tête : un numéro de
          rue en quatorze points, entre deux lignes grises, ne se lit pas d'un
          coup d'œil. Le nom passe au-dessus, plus petit — on sait déjà chez qui
          on va, on cherche où.
        */}
        <Card className="gap-4">
          <View className="gap-1">
            <Text className="text-[14px] text-muted-foreground">
              {toSender ? t('parcel.sender') : t('parcels.client')}
            </Text>
            <Text className="text-[18px] font-semibold text-foreground">{stop.name}</Text>
          </View>

          <View className="gap-1">
            <Text className="text-[22px] font-bold leading-7 text-foreground">{stop.address}</Text>
            {stop.area ? (
              <Text className="text-[16px] text-muted-foreground">{stop.area}</Text>
            ) : null}
          </View>

          {toSender ? null : (
            <Text className="text-[17px] font-semibold tabular-nums text-foreground">
              {t('parcel.collectLong')} · {(parcel.codToCollect / 100).toFixed(2)} MAD
            </Text>
          )}

          {/* Les deux boutons ronds de la maquette, et le WhatsApp qui est,
              au Maroc, souvent le seul canal auquel on répond. */}
          <View className="flex-row gap-2">
            {/* D-244 : jusqu'à trois numéros, UN bouton par numéro ; WhatsApp
                sur le premier. */}
            {phonesOf(stop.phone).map((phone, _index, phones) => (
              <RoundAction
                key={phone}
                label={phones.length > 1 ? t('run.callNumber', { phone }) : t('run.call')}
                icon={Phone}
                onPress={() => void Linking.openURL(`tel:${phone}`)}
              />
            ))}
            <RoundAction
              label={t('parcel.whatsapp')}
              icon={MessageCircle}
              onPress={() =>
                void Linking.openURL(`https://wa.me/${waNumber(phonesOf(stop.phone)[0] ?? '')}`)
              }
            />
            <RoundAction
              label={t('run.route')}
              icon={Navigation}
              onPress={() => void Linking.openURL(stop.directionsUrl)}
            />
          </View>
        </Card>

        <Button
          label={t('circuits.openRoute')}
          icon={Navigation}
          tone="ghost"
          onPress={() => void Linking.openURL(stop.directionsUrl)}
          full
        />

        <Button
          label={t('circuits.arrived')}
          icon={CircleCheck}
          onPress={() =>
            router.replace(`/(courier)/parcel/${encodeURIComponent(parcel.code)}?report=1`)
          }
          full
        />
      </View>
    </Screen>
  );
}

/** Un bouton rond de 44 points : on le touche avec des gants, au guidon. */
function RoundAction({
  label,
  icon: Icon,
  onPress,
}: {
  label: string;
  icon: typeof Phone;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      onPress={onPress}
      className="h-12 w-12 items-center justify-center rounded-full bg-muted"
      style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
    >
      <Icon size={22} className="text-foreground" />
    </Pressable>
  );
}
