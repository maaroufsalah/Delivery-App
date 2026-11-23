import type { VehicleType } from '@delivery/shared';
import { useRouter } from 'expo-router';
import {
  Bike,
  Car,
  ChevronRight,
  History,
  Languages,
  LifeBuoy,
  Truck,
  TrendingUp,
  User,
} from 'lucide-react-native';
import type { LucideIcon } from 'lucide-react-native';
import { useState } from 'react';
import { Linking, Pressable, ScrollView, Text, View } from 'react-native';
import { useI18n, type MessageKey } from '@/i18n';
import { useMe } from '@/lib/courier';
import { useSession } from '@/lib/session';
import { useTenantTheme } from '@/theme/tenant-theme';
import { Card, Divider, Screen, Sheet, SheetItem } from '@/ui';

/**
 * Une icône par véhicule : c'est la première chose qu'on reconnaît.
 *
 * Les clés sont les trois valeurs de `VEHICLE_TYPES` telles qu'elles voyagent
 * sur le fil — minuscules. Le repli existe pour le jour où une quatrième est
 * ajoutée : une icône générique vaut mieux qu'une ligne vide.
 */
const VEHICLE_ICONS: Record<VehicleType, LucideIcon> = {
  motorbike: Bike,
  car: Car,
  van: Truck,
};

/**
 * LE PROFIL DU LIVREUR — L2 bloc 8, maquette 12.
 *
 * **Un sommaire, pas un formulaire.** L'écran portait tout — langue, thème,
 * biométrie, notifications, pages légales, suppression de compte — sur une
 * seule colonne de huit cartes. Anass le réduit à cinq lignes, et il a raison :
 * on ouvre son profil pour ALLER quelque part, pas pour lire ses réglages.
 *
 * **Mon véhicule ne demandait aucune migration** : `Courier.vehicleType` existe
 * depuis M1 et `GET /courier/me` le rend. Il manquait seulement à l'écran — ce
 * que l'audit d'écart avait relevé (ligne 12).
 *
 * **« Mes statistiques » et « Historique » ne mènent plus au même endroit.**
 * D-L2-3 les avait fondus faute de destination ; le bloc 6 a créé la
 * facturation, qui porte « Historique des transactions ». Les statistiques
 * répondent à « comment je travaille », l'historique à « où est passé
 * l'argent » — deux questions, deux écrans.
 */
export default function CourierProfile() {
  const { t } = useI18n();
  const router = useRouter();
  const { profile } = useSession();
  const { brand } = useTenantTheme();

  const me = useMe();
  const [help, setHelp] = useState(false);
  const [vehicle, setVehicle] = useState(false);

  const VehicleIcon = me.data ? (VEHICLE_ICONS[me.data.vehicleType] ?? Truck) : Truck;
  const vehicleLabel = me.data ? t(`vehicle.${me.data.vehicleType}` as MessageKey) : '—';

  return (
    <Screen>
      <Text className="px-5 pb-3 pt-4 text-[22px] font-bold text-foreground">
        {t('profile.title')}
      </Text>

      <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-3 px-5 pb-8">
        {/* --- La carte d'identité de la maquette --- */}
        <Card className="flex-row items-center gap-4">
          <View className="bg-accent h-16 w-16 items-center justify-center rounded-full">
            <Text className="text-accent-foreground text-[22px] font-bold">
              {initials(profile ? `${profile.firstName} ${profile.lastName}` : (brand?.name ?? ''))}
            </Text>
          </View>

          <View className="flex-1 gap-0.5">
            <Text className="text-[19px] font-semibold text-foreground" numberOfLines={1}>
              {profile ? `${profile.firstName} ${profile.lastName}` : '—'}
            </Text>
            <Text className="text-[15px] text-muted-foreground">{t('home.role')}</Text>
            {me.data ? (
              <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
                {[me.data.agencyName, me.data.cityName].filter(Boolean).join(' · ')}
              </Text>
            ) : null}
          </View>
        </Card>

        <Card className="gap-1 p-1">
          <Row
            icon={User}
            label={t('profile.myInfo')}
            onPress={() => router.push('/(courier)/account')}
          />
          <Divider />
          <Row
            icon={VehicleIcon}
            label={t('profile.myVehicle')}
            value={me.data ? vehicleLabel : undefined}
            onPress={() => setVehicle(true)}
          />
          <Divider />
          <Row
            icon={TrendingUp}
            label={t('stats.title')}
            onPress={() => router.push('/(courier)/stats')}
          />
          <Divider />
          <Row
            icon={History}
            label={t('profile.history')}
            onPress={() => router.push('/(courier)/billing')}
          />
          <Divider />
          {/* Langue et thème depuis « Mon profil » — recette 4, point 3. Les
              contrôles restent ceux de Réglages : un seul endroit les porte. */}
          <Row
            icon={Languages}
            label={t('profile.languageTheme')}
            onPress={() => router.push('/(courier)/settings')}
          />
          <Divider />
          <Row icon={LifeBuoy} label={t('profile.help')} onPress={() => setHelp(true)} />
        </Card>
      </ScrollView>

      {/*
        MON VÉHICULE, en feuille plutôt qu'en écran.

        C'est UNE valeur, et elle ne se modifie pas depuis le téléphone : le
        véhicule est posé par l'agence sur la fiche livreur. Un écran pour la
        lire aurait été une page blanche avec une ligne ; une feuille dit la
        même chose et se referme d'un geste.
      */}
      <Sheet open={vehicle} onClose={() => setVehicle(false)} title={t('profile.myVehicle')}>
        <View className="gap-1 px-2 pb-3">
          <View className="flex-row items-center gap-3 py-2">
            <VehicleIcon size={22} className="text-primary" />
            <Text className="flex-1 text-[17px] font-semibold text-foreground">{vehicleLabel}</Text>
          </View>
          <Text className="text-[14px] text-muted-foreground">{t('profile.vehicleHint')}</Text>
        </View>
      </Sheet>

      {/*
        AIDE & SUPPORT : le numéro de l'agence, pas un formulaire.

        Un livreur bloqué devant une porte appelle — il ne remplit pas un
        ticket. Les coordonnées viennent de la société (`/public/company`), déjà
        chargée pour le thème : aucun appel de plus.
      */}
      <Sheet open={help} onClose={() => setHelp(false)} title={t('profile.help')}>
        <View className="pb-2">
          {brand?.phone ? (
            <SheetItem
              icon={LifeBuoy}
              label={brand.phone}
              hint={brand.name}
              onPress={() => {
                setHelp(false);
                void Linking.openURL(`tel:${brand.phone ?? ''}`);
              }}
            />
          ) : (
            <Text className="px-3 py-3 text-[15px] text-muted-foreground">
              {t('profile.noSupport')}
            </Text>
          )}
        </View>
      </Sheet>
    </Screen>
  );
}

/** Les initiales, faute de photo : deux lettres valent mieux qu'une silhouette. */
function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('');
}

/** Une ligne de sommaire : icône, libellé, valeur facultative, chevron. */
function Row({
  icon: Icon,
  label,
  value,
  onPress,
}: {
  icon: LucideIcon;
  label: string;
  value?: string;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      onPress={onPress}
      className="min-h-[52px] flex-row items-center gap-3 rounded-2xl px-3"
      style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
    >
      <Icon size={20} className="text-muted-foreground" />
      <Text className="flex-1 text-[16px] text-foreground" numberOfLines={1}>
        {label}
      </Text>
      {value ? <Text className="text-[15px] text-muted-foreground">{value}</Text> : null}
      <ChevronRight size={18} className="flip-rtl text-muted-foreground" />
    </Pressable>
  );
}
