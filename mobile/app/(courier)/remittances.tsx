import * as Haptics from 'expo-haptics';
import { Banknote, Lock } from 'lucide-react-native';
import { useRef, useState } from 'react';
import { Alert, FlatList, RefreshControl, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import type { RemittanceDetail, RemittanceListItem, RemittanceStage } from '@delivery/shared';
import { ActionFeedbackLine, type Feedback } from '@/components/action-feedback';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n } from '@/i18n';
import {
  useClosedRemittances,
  useCloseRemittance,
  useOpenRemittance,
  useRemittances,
} from '@/lib/courier';
import { describeFailure } from '@/lib/failure';
import { formatStamp } from '@/lib/datetime';
import { pending as pendingQueue } from '@/lib/queue';
import {
  canCloseRemittance,
  REMITTANCE_STAGE_LABEL,
  RemittanceCloseBlockedError,
  remittanceStageOf,
  remittanceStageTone,
} from '@/lib/remittance-close';
import { useTokens } from '@/theme/tenant-theme';
import { Button, Card, EmptyState, Skeleton, StatusBadge } from '@/components/ui';
import { useToast } from '@/ui';

/**
 * MA CAISSE — M1, étendu par le chantier « Retour de fonds unique », point 7,
 * puis par « Clôture de caisse par le livreur » (D-243).
 *
 * Trois questions, dans l'ordre du terrain :
 *
 *  1. **« Qu'est-ce que je porte ? »** : la caisse OUVERTE, en tête, avec ses
 *     colis et ce qu'il devra poser sur la table. En fin de journée, il la
 *     CLÔTURE lui-même : la carte se vide, la prochaine livraison ouvrira une
 *     nouvelle caisse.
 *  2. **« Qu'est-ce que je dois encore remettre ? »** : ses caisses FERMÉES,
 *     que l'agence n'a pas encore reçues.
 *  3. **« Est-ce que c'est passé ? »** : les caisses reçues, puis validées.
 *
 * **Compter et confirmer restent des gestes d'agence** (D-102, D-117) : le
 * livreur ferme, il n'atteste rien.
 *
 * **Le statut affiché vient du serveur** (`stage`) : l'écran n'en dérive aucun.
 *
 * La portée serveur le borne à SES caisses, et l'API ne sait rien de cet écran.
 */
export default function RemittancesScreen() {
  const { t } = useI18n();
  const { data, isLoading, refetch } = useRemittances();
  const open = useOpenRemittance();
  const closed = useClosedRemittances();
  const pull = usePullToRefresh({ refetch }, open, closed);

  if (isLoading) {
    return (
      <SafeAreaView className="flex-1 gap-3 bg-background px-5 pt-4" edges={['top']}>
        <Skeleton className="h-24" />
        <Skeleton className="h-24" />
      </SafeAreaView>
    );
  }

  const items = data?.items ?? [];

  return (
    <SafeAreaView className="flex-1 bg-background" edges={['top']}>
      <Text className="px-5 pb-3 pt-4 text-[22px] font-bold text-foreground">
        {t('remittances.title')}
      </Text>

      <FlatList
        keyboardShouldPersistTaps="handled"
        data={items}
        keyExtractor={(item) => item.id}
        contentContainerClassName="gap-2 px-5 pb-6"
        refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
        ListHeaderComponent={
          <View className="gap-2 pb-2">
            <OpenRemittance detail={open.data ?? null} />
            <ClosedRemittances items={closed.data?.items ?? []} />

            <Text className="pt-2 text-[17px] font-bold text-foreground">
              {t('remittances.historyTitle')}
            </Text>
          </View>
        }
        ListEmptyComponent={<EmptyState icon={Banknote} title={t('remittances.historyEmpty')} />}
        renderItem={({ item }) => <RemittanceRow item={item} />}
      />
    </SafeAreaView>
  );
}

/**
 * LA COULEUR D'UN STATUT DE CAISSE.
 *
 * Des jetons du thème, lus en valeur : React Native ne résout pas les
 * variables CSS, et `StatusBadge` teinte son fond en ligne (défaut de M1, un
 * badge sans fond ni couleur). Le choix du jeton vit dans
 * `remittanceStageTone`, partagé avec l'écran de l'agence.
 */
function useStageColor(): (stage: RemittanceStage) => string {
  const tokens = useTokens();

  return (stage) => tokens[remittanceStageTone(stage)];
}

/** Une caisse de l'historique ou des fermées : code, ville, montant, statut. */
function RemittanceRow({ item }: { item: RemittanceListItem }) {
  const { t, locale } = useI18n();
  const colorOf = useStageColor();
  const stage = remittanceStageOf(item);

  return (
    <Card className="gap-2">
      <View className="flex-row items-start justify-between gap-3">
        <View className="flex-1 gap-0.5">
          <Text className="font-mono text-[15px] text-foreground">{item.code}</Text>
          <Text className="text-[14px] text-muted-foreground">
            {item.cityName} · {item.parcelCount}
          </Text>
        </View>

        <Text className="text-[19px] font-bold tabular-nums text-foreground">
          {(item.amount / 100).toFixed(2)} MAD
        </Text>
      </View>

      <View className="flex-row flex-wrap items-center justify-between gap-2">
        <StatusBadge label={t(REMITTANCE_STAGE_LABEL[stage])} color={colorOf(stage)} />

        {stage === 'closed' && item.courierClosedAt ? (
          <Text className="text-[13px] text-muted-foreground">
            {t('remittances.closedAt', {
              date: formatStamp(item.courierClosedAt, locale),
            })}
          </Text>
        ) : null}
      </View>
    </Card>
  );
}

/**
 * MES CAISSES FERMÉES — D-243. L'argent qu'il a encore sur lui et que l'agence
 * attend. Rien à afficher quand il n'en a pas : un titre au-dessus d'un vide
 * ferait chercher une caisse qui n'existe pas.
 */
function ClosedRemittances({ items }: { items: RemittanceListItem[] }) {
  const { t } = useI18n();

  if (items.length === 0) return null;

  return (
    <View className="gap-2 pt-2">
      <View className="gap-0.5">
        <Text className="text-[17px] font-bold text-foreground">
          {t('remittances.closedTitle')}
        </Text>
        <Text className="text-[13px] text-muted-foreground">{t('remittances.closedHint')}</Text>
      </View>

      {items.map((item) => (
        <RemittanceRow key={item.id} item={item} />
      ))}
    </View>
  );
}

/**
 * CE QUE JE PORTE — point 7, et le geste de clôture (D-243).
 *
 * Les trois chiffres qu'il vient chercher, et la liste des colis qui les
 * explique. Ils arrivent calculés du serveur : l'écran n'en dérive aucun.
 */
function OpenRemittance({ detail }: { detail: RemittanceDetail | null }) {
  const { t } = useI18n();
  const colorOf = useStageColor();
  const toast = useToast();
  const close = useCloseRemittance();

  const [closing, setClosing] = useState(false);
  const [feedback, setFeedback] = useState<Feedback | null>(null);
  /* La garde SYNCHRONE : un double appui ne clôture qu'une fois (D-151). */
  const busy = useRef(false);

  const money = (cents: number) => `${(cents / 100).toFixed(2)} MAD`;

  /** Le refus de la file, dit avec la même phrase d'où qu'il vienne. */
  const queueBlocked = (): Feedback => ({
    tone: 'warn',
    severity: 'business',
    text: t('remittances.closeQueueBlocked'),
  });

  /*
   * L'ENVOI, après confirmation. En ligne, jamais mis en file : un échec
   * réseau s'affiche ici et le livreur réessaie. Le hook revérifie la file
   * juste avant l'appel (`assertCanCloseRemittance`).
   */
  const confirmClose = () => {
    if (busy.current) return;
    busy.current = true;
    setClosing(true);
    const release = () => {
      busy.current = false;
      setClosing(false);
    };

    close
      .mutateAsync()
      .then(() => {
        void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success).catch(
          () => undefined,
        );
        toast.show(t('remittances.closeDone'));
      })
      .catch((error: unknown) => {
        void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error).catch(
          () => undefined,
        );
        setFeedback(
          error instanceof RemittanceCloseBlockedError ? queueBlocked() : describeFailure(error, t),
        );
      })
      .finally(release);
  };

  /*
   * L'APPUI. Le bouton reste visible même quand la clôture est impossible :
   * c'est l'appui qui explique le refus, un bouton absent ne dirait rien.
   *
   * La file hors ligne est relue À CET INSTANT : tant qu'une livraison attend
   * d'être envoyée, le serveur ne la connaît pas et la caisse serait fermée
   * sans elle. Aucune confirmation n'est proposée dans ce cas.
   */
  const requestClose = () => {
    if (busy.current || !detail) return;
    setFeedback(null);

    void pendingQueue().then((queued) => {
      if (!canCloseRemittance(queued.length).allowed) {
        setFeedback(queueBlocked());
        return;
      }

      Alert.alert(
        t('remittances.closeConfirmTitle'),
        t('remittances.closeConfirmBody', { amount: money(detail.expectedNet) }),
        [
          { text: t('common.cancel'), style: 'cancel' },
          { text: t('remittances.close'), onPress: confirmClose },
        ],
      );
    });
  };

  return (
    <View className="gap-2">
      <Text className="text-[17px] font-bold text-foreground">{t('remittances.openTitle')}</Text>

      {detail === null ? (
        <Card>
          <Text className="text-center text-[14px] text-muted-foreground">
            {t('remittances.openEmpty')}
          </Text>
        </Card>
      ) : (
        <Card className="gap-3">
          <View className="flex-row flex-wrap items-center justify-between gap-2">
            <Text className="font-mono text-[15px] text-foreground">{detail.code}</Text>
            <StatusBadge label={t(REMITTANCE_STAGE_LABEL.open)} color={colorOf('open')} />
          </View>

          <View className="gap-1.5">
            <Figure label={t('remittances.collected')} value={money(detail.expectedCod)} />
            <Figure label={t('remittances.commission')} value={money(detail.expectedCommission)} />
            <Figure label={t('remittances.toHandOver')} value={money(detail.expectedNet)} strong />
          </View>

          {/*
            LES PAPIERS, À PART — chantier « Chèques », point 1. Les
            additionner au net ferait croire au livreur qu'il doit sortir de sa
            poche l'argent du vendeur.
          */}
          {detail.cheques.length > 0 ? (
            <View className="bg-muted/40 gap-1 rounded-lg p-3">
              <View className="flex-row items-center justify-between gap-3">
                <Text className="text-[14px] font-semibold text-foreground">
                  {t('remittances.chequesToHandOver')}
                </Text>
                <Text className="text-[15px] font-bold tabular-nums text-foreground">
                  {money(detail.totalCheque)}
                </Text>
              </View>

              {detail.cheques.map((cheque) => (
                <View key={cheque.id} className="flex-row items-center justify-between gap-3">
                  <Text className="flex-1 font-mono text-[13px] text-muted-foreground">
                    {cheque.number}
                  </Text>
                  <Text className="text-[13px] tabular-nums text-foreground">
                    {money(cheque.amount)}
                  </Text>
                </View>
              ))}
            </View>
          ) : null}

          <View className="gap-1">
            {detail.items.map((item) => (
              <View key={item.parcelId} className="flex-row items-center justify-between gap-3">
                <Text className="flex-1 font-mono text-[13px] text-muted-foreground">
                  {item.parcelCode}
                </Text>
                <Text className="text-[13px] tabular-nums text-foreground">
                  {money(item.amount)}
                </Text>
              </View>
            ))}
          </View>

          <Text className="text-[13px] text-muted-foreground">{t('remittances.closeHint')}</Text>

          <Button
            label={t('remittances.close')}
            icon={Lock}
            onPress={requestClose}
            busy={closing}
            full
          />

          {/* Le refus PRÈS DU BOUTON, dans la carte : pas sous une fenêtre. */}
          {feedback ? <ActionFeedbackLine feedback={feedback} /> : null}
        </Card>
      )}
    </View>
  );
}

function Figure({
  label,
  value,
  strong = false,
}: {
  label: string;
  value: string;
  strong?: boolean;
}) {
  return (
    <View className="flex-row items-center justify-between gap-3">
      <Text className="text-[14px] text-muted-foreground">{label}</Text>
      <Text
        className={
          strong
            ? 'text-[17px] font-bold tabular-nums text-foreground'
            : 'text-[15px] tabular-nums text-foreground'
        }
      >
        {value}
      </Text>
    </View>
  );
}
