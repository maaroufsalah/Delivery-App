import { RETURN_HANDOVER_STATUS_COLORS, type ReturnHandoverStatus } from '@delivery/shared';
import { useLocalSearchParams } from 'expo-router';
import { PackageCheck, PackageX, Truck } from 'lucide-react-native';
import { RefreshControl, ScrollView, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { CartonTag, CartonTotal } from '@/components/carton-count';
import { useI18n, type MessageKey } from '@/i18n';
import { describeActionError } from '@/lib/use-action';
import { useHandover, useHandoverActions } from '@/lib/courier';
import { formatStamp } from '@/lib/datetime';
import {
  Button,
  Card,
  EmptyState,
  Screen,
  ScreenHeader,
  SkeletonCard,
  StatusBadge,
  Timeline,
  useToast,
} from '@/ui';

/**
 * UN BON DE RETOUR SUR LE TÉLÉPHONE — chantier « Retours, BL Clients Pro »,
 * point 3 : « liste de ses bons de retour (créés par lui et par l'admin),
 * création, détail, PDF, prise en charge, remise ».
 *
 * **Les deux gestes sont ceux du terrain, et ils sont séparés.** « Je l'ai » se
 * fait devant les cartons, souvent dans une rue ; « je l'ai déposée » se fait
 * sur le quai, vingt minutes plus tard. Les confondre en un seul bouton — ce
 * que faisait le legacy — rend invisible tout ce qui se perd entre les deux.
 *
 * **Ce que l'écran n'offre pas, et c'est voulu.** Aucune confirmation de
 * réception : celui qui rapporte les colis n'atteste pas lui-même les avoir
 * rendus. Le rôle `COURIER` porte `returns.write` et pas `returns.confirm`, et
 * un bouton qui échouerait toujours serait une promesse fausse.
 *
 * **Les boutons viennent du SERVEUR.** `pickable` et `handoverable` sont
 * calculés par l'API ; recopier la machine à états ici ferait une troisième
 * version d'une règle qui en a déjà deux à tenir d'accord.
 */
export default function CourierHandover() {
  const { code } = useLocalSearchParams<{ code: string }>();
  const { t, locale } = useI18n();
  const toast = useToast();

  const handover = useHandover(code);
  const pull = usePullToRefresh(handover);
  const { pickup, handOver } = useHandoverActions();

  if (handover.isLoading) {
    return (
      <Screen>
        <ScreenHeader title={code ?? ''} backHref={'/(courier)/returns'} />
        <View className="gap-3 px-5">
          <SkeletonCard />
          <SkeletonCard />
        </View>
      </Screen>
    );
  }

  const data = handover.data;

  if (!data) {
    return (
      <Screen>
        <ScreenHeader title={code ?? ''} backHref={'/(courier)/returns'} />
        <View className="px-5">
          <EmptyState
            art="box"
            icon={PackageX}
            title={t('handovers.empty')}
            hint={t('handovers.emptyHint')}
          />
        </View>
      </Screen>
    );
  }

  const statusLabel = (status: ReturnHandoverStatus): string =>
    t(`handovers.status.${status}` as MessageKey);

  const busy = pickup.isPending || handOver.isPending;

  return (
    <Screen>
      <ScreenHeader title={data.code} backHref={'/(courier)/returns'} />

      <ScrollView
        keyboardShouldPersistTaps="handled"
        contentContainerClassName="gap-3 px-5 pb-8"
        refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
      >
        <Card className="gap-2">
          <View className="flex-row items-center justify-between gap-3">
            <Text className="flex-1 font-mono text-[16px] font-bold text-foreground">
              {data.code}
            </Text>
            <StatusBadge
              label={statusLabel(data.status)}
              color={RETURN_HANDOVER_STATUS_COLORS[data.status]}
            />
          </View>

          {/*
            « Le bon apparaît chez le livreur avec la mention Créé par
            l'admin » — point 2. C'est elle qui lui dit qu'il exécute une
            demande, et non sa propre liste.
          */}
          <Text className="text-[14px] text-muted-foreground">
            {data.origin === 'admin'
              ? t('handovers.createdByAdmin')
              : t('handovers.createdByCourier')}
            {' · '}
            {t('handovers.parcels', { count: data.parcelCount })}
          </Text>

          {data.clientNames.length > 0 ? (
            <Text className="text-[13px] text-muted-foreground" numberOfLines={2}>
              {data.clientNames.join(', ')}
            </Text>
          ) : null}

          {data.origin === 'admin' && !data.editable ? (
            <Text className="text-[13px] text-muted-foreground">{t('handovers.locked')}</Text>
          ) : null}
        </Card>

        {/* « 3 colis · 6 cartons » — le bon, cartons compris. */}
        <CartonTotal lines={data.items} />

        {data.items.length === 0 ? (
          <EmptyState art="box" icon={PackageX} title={t('handovers.noItems')} hint="" />
        ) : (
          data.items.map((item) => (
            <Card key={item.id} className="gap-1">
              <Text className="font-mono text-[15px] font-bold text-foreground">
                {item.parcelCode}
              </Text>
              <Text className="text-[13px] text-muted-foreground" numberOfLines={1}>
                {item.recipientName} · {item.clientName}
              </Text>
              <CartonTag line={item} />
            </Card>
          ))
        )}

        {/* --- Le journal, même contenu que le web (point 1) ------------- */}
        <Text className="pt-2 text-[13px] font-semibold text-muted-foreground">
          {t('handovers.journal')}
        </Text>
        <Timeline
          entries={data.steps.map((step) => ({
            id: step.id,
            label: statusLabel(step.status),
            at: formatStamp(step.createdAt, locale),
            author: step.authorName,
            comment: step.comment,
            color: RETURN_HANDOVER_STATUS_COLORS[step.status],
          }))}
        />

        {data.pickable ? (
          <Button
            label={t('handovers.pickup')}
            icon={Truck}
            busy={pickup.isPending}
            disabled={busy}
            onPress={() => {
              pickup.mutate(
                { code: data.code },
                {
                  onSuccess: () => toast.show(t('handovers.pickupDone')),
                  onError: (error: Error) => toast.show(describeActionError(error, t), 'error'),
                },
              );
            }}
          />
        ) : null}

        {data.handoverable ? (
          <Button
            label={t('handovers.handover')}
            icon={PackageCheck}
            busy={handOver.isPending}
            disabled={busy}
            onPress={() => {
              handOver.mutate(data.code, {
                onSuccess: () => toast.show(t('handovers.handoverDone')),
                onError: (error: Error) => toast.show(describeActionError(error, t), 'error'),
              });
            }}
          />
        ) : null}
      </ScrollView>
    </Screen>
  );
}
