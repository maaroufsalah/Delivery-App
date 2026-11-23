import {
  normalizePhone,
  pickupNoteState,
  routeTargetsSender,
  type CourierPickupNote,
  type PickupNoteState,
} from '@delivery/shared';
import { Navigation, PackageSearch, Phone } from 'lucide-react-native';
import { useState } from 'react';
import { FlatList, Linking, Pressable, RefreshControl, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n } from '@/i18n';
import { usePickups, useRoutes } from '@/lib/courier';
import { useRefetchOnFocus } from '@/lib/use-refetch-on-focus';
import { CircuitCard } from '@/components/circuit-card';
import { useTokens } from '@/theme/tenant-theme';
import { Card, Chip, EmptyState, FadeIn, Screen, SkeletonCard, StatusBadge } from '@/ui';

/** Les trois onglets de la maquette 7, dans son ordre. */
const TABS: {
  key: PickupNoteState;
  label: 'pickups.pending' | 'pickups.running' | 'pickups.done';
}[] = [
  { key: 'pending', label: 'pickups.pending' },
  { key: 'in-progress', label: 'pickups.running' },
  { key: 'done', label: 'pickups.done' },
];

/**
 * LA GESTION DES RAMASSAGES — L2 bloc 4, maquette 7.
 *
 * **Ce que c'est, dans le vocabulaire du domaine.** « Ramassage » chez Anass
 * est le **bon de ramassage** : un `DeliveryNote` de type `PICKUP`
 * (DOMAIN.md §1.12). Le livreur y est relié par ses COLIS — un bon ne porte pas
 * de `courierId` — et c'est ce lien, plus étroit qu'une permission, qui le
 * borne à ce qui le concerne (D-082).
 *
 * **Trois onglets pour deux états.** Le bon est « à valider » ou « validé » ;
 * la maquette demande En attente, En cours, Terminés. Le troisième est un état
 * du TRAVAIL, pas du bon : il se déduit des colis (`pickupNoteState`), sans
 * migration ni statut inventé.
 *
 * **L'écran DISPARAÎT tant que la route n'est pas déployée.**
 * `GET /courier/me/notes` est l'un des trois endpoints de ce chantier, et la
 * production ne les a pas : sur un 404 on affiche un état vide qui le dit,
 * jamais une erreur et jamais un plantage. C'est la règle de la mission.
 *
 * **Pas de bouton flottant.** La maquette en montre un, « + », dont la
 * transcription note elle-même qu'on ne sait pas ce qu'il ouvre. Les deux
 * gestes qu'un livreur fait devant un ramassage qui coince — appeler le
 * client, ouvrir l'itinéraire — sont sur chaque carte, où on les atteint sans
 * viser. La question reste posée au PO (D-L2-6).
 */
export default function CourierPickups() {
  const { t } = useI18n();
  const [tab, setTab] = useState<PickupNoteState>('pending');

  const pickups = usePickups('pickup');

  /*
   * LES CIRCUITS DE RAMASSAGE — Recette 5, point 1. Ils étaient sous
   * « Livraison », mêlés aux autres ; ils vivent ici, au-dessus des bons qu'ils
   * portent. Ouverts seulement (point 2) : brouillon ou parti.
   */
  const active = useRoutes('active');
  const draft = useRoutes('draft');
  const pull = usePullToRefresh(pickups, active, draft);
  // Un circuit clos par l'agence quitte la liste au retour sur l'écran.
  useRefetchOnFocus(active, draft, pickups);
  const circuits = [...(active.data ?? []), ...(draft.data ?? [])].filter((route) =>
    routeTargetsSender(route.kind),
  );

  const notes = pickups.data ?? [];
  const shown = notes.filter((note) => pickupNoteState(note) === tab);

  /* Un compteur par onglet : on choisit son onglet SACHANT ce qu'il contient. */
  const countOf = (state: PickupNoteState) =>
    notes.filter((note) => pickupNoteState(note) === state).length;

  return (
    <Screen>
      <Text className="px-5 pb-3 pt-4 text-[22px] font-bold text-foreground">
        {t('pickups.title')}
      </Text>

      {circuits.length > 0 ? (
        <View className="gap-3 px-5 pb-3">
          {circuits.map((route) => (
            <CircuitCard key={route.id} route={route} />
          ))}
        </View>
      ) : null}

      <View className="flex-row gap-2 px-5 pb-3">
        {TABS.map((entry) => (
          <Chip
            key={entry.key}
            label={t(entry.label)}
            count={countOf(entry.key)}
            active={tab === entry.key}
            onPress={() => setTab(entry.key)}
          />
        ))}
      </View>

      {pickups.isLoading ? (
        <View className="gap-3 px-5">
          <SkeletonCard />
          <SkeletonCard />
        </View>
      ) : (
        <FlatList
          keyboardShouldPersistTaps="handled"
          data={shown}
          keyExtractor={(note) => note.id}
          contentContainerClassName="gap-3 px-5 pb-6"
          refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
          ListEmptyComponent={
            <EmptyState
              art="box"
              icon={PackageSearch}
              title={t(pickups.isError ? 'pickups.unavailable' : 'pickups.empty')}
              hint={t(pickups.isError ? 'pickups.unavailableHint' : 'pickups.emptyHint')}
            />
          }
          renderItem={({ item, index }) => (
            <FadeIn index={index}>
              <PickupCard note={item} />
            </FadeIn>
          )}
        />
      )}
    </Screen>
  );
}

/**
 * La carte de la maquette : le code, le badge d'état, le client, l'adresse.
 *
 * Les libellés « Client : » et « Adresse : » sont ceux d'Anass, et ceux de
 * l'écran Colis : deux écrans qui montrent la même chose ne devraient pas la
 * nommer de deux façons.
 */
function PickupCard({ note }: { note: CourierPickupNote }) {
  const { t } = useI18n();
  const tokens = useTokens();

  const state = pickupNoteState(note);

  /*
   * Les couleurs des trois états. Ce ne sont PAS des statuts de colis, donc
   * elles ne viennent pas du référentiel — ce sont les tons du thème, qui
   * suivent la marque du tenant et le mode sombre.
   *
   * Par `useTokens()` et non par une classe : `StatusBadge` teinte son fond en
   * ligne, et React Native ne résout PAS les variables CSS — `'var(--warning)'`
   * n'y est pas une couleur approximative, c'est une couleur absente (M4).
   */
  const tones: Record<PickupNoteState, string> = {
    pending: tokens.warning,
    'in-progress': tokens.primary,
    done: tokens.success,
  };

  const labels: Record<PickupNoteState, 'pickups.pending' | 'pickups.running' | 'pickups.done'> = {
    pending: 'pickups.pending',
    'in-progress': 'pickups.running',
    done: 'pickups.done',
  };

  return (
    <Card className="gap-2">
      <View className="flex-row items-center justify-between gap-3">
        <Text className="flex-1 font-mono text-[16px] font-bold text-foreground">{note.code}</Text>
        <StatusBadge label={t(labels[state])} color={tones[state]} />
      </View>

      <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
        {t('parcels.client')} : {note.clientName}
      </Text>
      <Text className="text-[14px] text-muted-foreground" numberOfLines={2}>
        {t('parcels.addressLabel')} : {note.address}, {note.cityName}
      </Text>
      <Text className="text-[14px] text-muted-foreground">
        {t('pickups.parcels', {
          done: note.pickedCount,
          total: note.parcelCount + note.removedCount,
        })}
      </Text>

      {/* Les deux gestes du terrain, à 44 points, sur la carte plutôt que
          derrière un bouton flottant qu'il faut viser d'une main. */}
      <View className="flex-row gap-2 pt-1">
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('run.call')}
          className="min-h-11 min-w-11 items-center justify-center rounded-xl bg-muted"
          hitSlop={8}
          onPress={() => void Linking.openURL(`tel:${normalizePhone(note.clientPhone)}`)}
        >
          <Phone size={20} className="text-foreground" />
        </Pressable>

        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('run.route')}
          className="min-h-11 min-w-11 items-center justify-center rounded-xl bg-muted"
          hitSlop={8}
          onPress={() =>
            void Linking.openURL(
              `https://maps.google.com/?q=${encodeURIComponent(`${note.address}, ${note.cityName}`)}`,
            )
          }
        >
          <Navigation size={20} className="text-foreground" />
        </Pressable>
      </View>
    </Card>
  );
}
