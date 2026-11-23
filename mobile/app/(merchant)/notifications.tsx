import { notificationCopy } from '@delivery/shared';
import { useRouter } from 'expo-router';
import { Bell, CheckCheck } from 'lucide-react-native';
import { FlatList, Pressable, RefreshControl, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n } from '@/i18n';
import { formatStamp } from '@/lib/datetime';
import {
  useMarkAllNotificationsRead,
  useMarkNotificationRead,
  useNotifications,
} from '@/lib/notifications-feed';
import { Card, EmptyState, FadeIn, Screen, ScreenHeader, SkeletonCard } from '@/ui';

/**
 * LE CENTRE DE NOTIFICATIONS — V2 vendeur bloc 8, spécification §10.
 *
 * **Ce qu'il change.** Jusqu'ici une notification était un message push :
 * envoyé, reçu ou perdu, et sans trace. Un vendeur qui n'avait pas son
 * téléphone en main quand son colis a été livré n'avait aucun moyen de
 * l'apprendre autrement qu'en parcourant sa liste de colis.
 *
 * **Le texte est rendu ICI, pas stocké.** La base garde le sujet et ses
 * paramètres ; `notificationCopy()` — le même catalogue que le push, depuis le
 * paquet partagé — en fait une phrase dans la langue du LECTEUR. Quelqu'un qui
 * passe son application en arabe relit tout son historique en arabe, et une
 * correction de formulation s'applique rétroactivement.
 *
 * **Toucher une ligne la marque lue ET l'ouvre.** Deux gestes pour une
 * intention unique — « je vais voir » — feraient revenir sur l'écran pour
 * acquitter ce qu'on vient de lire.
 */
export default function MerchantNotifications() {
  const { t, locale } = useI18n();
  const router = useRouter();

  const feed = useNotifications();
  const pull = usePullToRefresh(feed);
  const markRead = useMarkNotificationRead();
  const markAll = useMarkAllNotificationsRead();

  const items = feed.data?.items ?? [];

  /** Là où une notification mène, quand elle mène quelque part. */
  const open = (data: Record<string, string>) => {
    if (data.parcelCode) {
      router.push(`/(merchant)/parcel/${encodeURIComponent(data.parcelCode)}`);
      return;
    }

    if (data.claimCode) {
      router.push(`/(merchant)/claim/${encodeURIComponent(data.claimCode)}`);
    }
  };

  return (
    <Screen>
      <ScreenHeader
        title={t('notifications.title')}
        action={
          (feed.data?.unread ?? 0) > 0 ? (
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={t('notifications.markAll')}
              onPress={() => markAll.mutate()}
              className="min-h-11 min-w-11 items-center justify-center"
              hitSlop={8}
              style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
            >
              <CheckCheck size={22} className="text-foreground" />
            </Pressable>
          ) : undefined
        }
      />

      {feed.isLoading ? (
        <View className="gap-3 px-5">
          <SkeletonCard />
          <SkeletonCard />
        </View>
      ) : (
        <FlatList
          keyboardShouldPersistTaps="handled"
          data={items}
          keyExtractor={(item) => item.id}
          contentContainerClassName="gap-3 px-5 pb-6"
          refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
          ListEmptyComponent={
            <EmptyState
              icon={Bell}
              art="box"
              title={t('notifications.empty')}
              hint={t('notifications.emptyHint')}
            />
          }
          renderItem={({ item, index }) => {
            const copy = notificationCopy(item.topic, locale, item.params);

            return (
              <FadeIn index={index}>
                <Pressable
                  accessibilityRole="button"
                  onPress={() => {
                    if (!item.readAt) markRead.mutate(item.id);
                    open(item.data);
                  }}
                  style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
                >
                  {/* La bordure marque le NON-LU : une pastille se cherche, une
                      bordure se voit du coin de l'œil en faisant défiler. */}
                  <Card className={`gap-1 ${item.readAt ? '' : 'border border-primary'}`}>
                    <Text className="text-[16px] font-semibold text-foreground">
                      {copy?.title ?? item.topic}
                    </Text>
                    <Text className="text-[15px] text-muted-foreground">{copy?.body ?? ''}</Text>
                    <Text className="text-[13px] text-muted-foreground">
                      {formatStamp(item.createdAt, locale)}
                    </Text>
                  </Card>
                </Pressable>
              </FadeIn>
            );
          }}
        />
      )}
    </Screen>
  );
}
