import type { NoteKind } from '@delivery/shared';
import { NOTE_STATUS_COLORS } from '@delivery/shared';
import { ClipboardList } from 'lucide-react-native';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { FlatList, RefreshControl, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n, type MessageKey } from '@/i18n';
import { useDeliveryNotes } from '@/lib/merchant';
import { Card, Chip, EmptyState, FadeIn, Screen, SkeletonCard, StatusBadge } from '@/ui';

/**
 * LES BONS — V2 vendeur bloc 1, spécification §5 et §6.
 *
 * **Un seul écran pour les deux genres, et deux puces pour trancher.** La
 * spécification décrit deux sections, « Bons de ramassage » et « Bons de
 * dépôt », au fonctionnement identique — même création, même PDF, même
 * historique. Les séparer en deux onglets aurait coûté un cinquième pouce sur
 * une barre qui n'en a que cinq, pour une distinction que le vendeur fait en
 * une touche.
 *
 * **Aucune route neuve.** `GET /delivery-notes` existe depuis C7 et sert les
 * deux genres ; le vendeur porte `notes.read` depuis M2. L'écran manquait, pas
 * le droit — et la portée serveur le borne déjà à ses propres bons (D-082).
 *
 * La création, le PDF et le détail arrivent au bloc 4 : cet écran est la
 * liste, et il ne promet rien qu'il ne tienne.
 */
export default function MerchantNotes() {
  const { t } = useI18n();
  const router = useRouter();

  const [kind, setKind] = useState<NoteKind>('pickup');
  const notes = useDeliveryNotes(kind);
  const pull = usePullToRefresh(notes);

  const rows = notes.data?.items ?? [];

  return (
    <Screen>
      <Text className="px-5 pb-3 pt-4 text-[22px] font-bold text-foreground">
        {t('notes.title')}
      </Text>

      <View className="flex-row gap-2 px-5 pb-3">
        <Chip
          label={t('notes.pickup')}
          active={kind === 'pickup'}
          onPress={() => setKind('pickup')}
        />
        <Chip
          label={t('notes.dropoff')}
          active={kind === 'dropoff'}
          onPress={() => setKind('dropoff')}
        />
      </View>

      {notes.isLoading ? (
        <View className="gap-3 px-5">
          <SkeletonCard />
          <SkeletonCard />
        </View>
      ) : (
        <FlatList
          keyboardShouldPersistTaps="handled"
          data={rows}
          keyExtractor={(item) => item.id}
          contentContainerClassName="gap-3 px-5 pb-6"
          refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
          ListEmptyComponent={
            <EmptyState
              icon={ClipboardList}
              art="box"
              title={t('notes.empty')}
              hint={t('notes.emptyHint')}
            />
          }
          renderItem={({ item, index }) => (
            <FadeIn index={index}>
              <Card
                className="gap-2"
                onPress={() => router.push(`/(merchant)/note/${encodeURIComponent(item.code)}`)}
              >
                <View className="flex-row items-center justify-between gap-3">
                  <Text className="flex-1 font-mono text-[14px] text-muted-foreground">
                    {item.code}
                  </Text>
                  <StatusBadge
                    label={t(`noteStatus.${item.status}` as MessageKey)}
                    color={NOTE_STATUS_COLORS[item.status]}
                  />
                </View>

                {/*
                  Le NOMBRE de colis avant la ville : c'est ce qu'on vient
                  vérifier — « est-ce que mes douze colis sont bien dessus » —
                  et la ville, on la connaît déjà, c'est la sienne.
                */}
                <Text className="text-[16px] font-semibold text-foreground">
                  {t('notes.parcelCount', { count: item.parcelCount })}
                </Text>
                <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
                  {item.cityName}
                  {item.pickupWaived ? ` · ${t('notes.waived')}` : ''}
                </Text>
              </Card>
            </FadeIn>
          )}
        />
      )}
    </Screen>
  );
}
