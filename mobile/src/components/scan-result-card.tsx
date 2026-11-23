import { phonesOf, routeTargetsSender, type CourierRouteParcel } from '@delivery/shared';
import { ChevronRight, MapPin, Phone, X } from 'lucide-react-native';
import { Linking, Pressable, Text, View } from 'react-native';
import { CartonTag } from '@/components/carton-count';
import { useI18n } from '@/i18n';
import { routeStopOf } from '@/lib/route-stop';
import { Card } from '@/ui';

/**
 * LA CARTE APRÈS UN SCAN — Recette 5, point 3, sur le modèle de l'ancienne
 * appli Wimo ; ramassée par le chantier « Scanner ».
 *
 * Numéro du colis, nom, montant, téléphone avec bouton d'appel, adresse. Les
 * couleurs sont celles du TENANT (`primary`, orange chez SpeedyTrans) : aucune
 * n'est écrite ici.
 *
 * **Les actions n'y sont plus** (chantier « Scanner », point 2) : « Livrer » et
 * « Autre statut » vivent dans LA rangée du pied de l'écran, à la place de
 * « Démarrer ma tournée » — jamais les deux à la fois, jamais l'une sur
 * l'autre. Toucher la carte ouvre la fiche du colis.
 */
export function ScanResultCard({
  parcel,
  kind,
  routeCode,
  tone,
  message,
  onOpen,
  onClose,
}: {
  parcel: CourierRouteParcel;
  kind: string;
  routeCode: string;
  tone: 'ok' | 'warn';
  message: string;
  onOpen: () => void;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const stop = routeStopOf(kind, parcel);
  const toSender = routeTargetsSender(kind);
  const phones = phonesOf(stop.phone);

  return (
    <Card className={`gap-2.5 border-2 ${tone === 'ok' ? 'border-success' : 'border-primary'}`}>
      <View className="flex-row items-start gap-2">
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('scanCard.open')}
          onPress={onOpen}
          className="flex-1 gap-0.5"
        >
          <Text
            className={`text-[14px] font-semibold ${tone === 'ok' ? 'text-success' : 'text-muted-foreground'}`}
          >
            {message}
          </Text>
          <View className="flex-row items-center gap-1">
            <Text className="font-mono text-[19px] font-bold text-foreground">{parcel.code}</Text>
            <ChevronRight size={18} className="flip-rtl text-muted-foreground" />
          </View>
          <Text className="text-[13px] text-muted-foreground">{routeCode}</Text>
          {/* « × 4 cartons » : un code, plusieurs cartons à prendre ou à remettre. */}
          <CartonTag line={parcel} />
        </Pressable>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('common.close')}
          onPress={onClose}
          className="min-h-11 min-w-11 items-center justify-center"
          hitSlop={8}
        >
          <X size={20} className="text-muted-foreground" />
        </Pressable>
      </View>

      <View className="flex-row items-center justify-between gap-3">
        <Text className="flex-1 text-[17px] font-semibold text-foreground" numberOfLines={2}>
          {stop.name}
        </Text>
        {toSender ? null : (
          <Text className="text-[20px] font-bold tabular-nums text-primary">
            {(parcel.codToCollect / 100).toFixed(2)} MAD
          </Text>
        )}
      </View>

      {/*
        D-244 : jusqu'à trois numéros. Un seul : la ligne d'avant, le numéro
        et « Appeler ». Plusieurs : UN bouton par numéro, qui montre le sien.
      */}
      {phones.length > 1 ? (
        <View className="flex-row flex-wrap gap-2">
          {phones.map((phone) => (
            <Pressable
              key={phone}
              accessibilityRole="button"
              accessibilityLabel={t('run.callNumber', { phone })}
              onPress={() => void Linking.openURL(`tel:${phone}`)}
              className="min-h-11 flex-row items-center gap-2 rounded-2xl bg-primary px-4"
              style={({ pressed }) => (pressed ? { opacity: 0.7 } : null)}
            >
              <Phone size={18} className="text-primary-foreground" />
              <Text className="text-[15px] font-semibold text-primary-foreground">{phone}</Text>
            </Pressable>
          ))}
        </View>
      ) : (
        <View className="flex-row items-center gap-3">
          <Text className="flex-1 text-[16px] text-foreground">{stop.phone}</Text>
          {phones.map((phone) => (
            <Pressable
              key={phone}
              accessibilityRole="button"
              accessibilityLabel={t('run.call')}
              onPress={() => void Linking.openURL(`tel:${phone}`)}
              className="min-h-11 flex-row items-center gap-2 rounded-2xl bg-primary px-4"
              style={({ pressed }) => (pressed ? { opacity: 0.7 } : null)}
            >
              <Phone size={18} className="text-primary-foreground" />
              <Text className="text-[15px] font-semibold text-primary-foreground">
                {t('run.call')}
              </Text>
            </Pressable>
          ))}
        </View>
      )}

      <View className="flex-row items-start gap-2">
        <MapPin size={16} className="mt-0.5 text-muted-foreground" />
        <Text className="flex-1 text-[15px] text-muted-foreground" numberOfLines={2}>
          {stop.area ? `${stop.address} · ${stop.area}` : stop.address}
        </Text>
      </View>
    </Card>
  );
}
