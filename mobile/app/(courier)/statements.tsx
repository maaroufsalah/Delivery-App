import { useLocalSearchParams, useRouter } from 'expo-router';
import { useFreshKey } from '@/lib/use-fresh-key';
import { Camera, Wallet } from 'lucide-react-native';
import { useState } from 'react';
import { FlatList, RefreshControl, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n } from '@/i18n';
import { useStatements } from '@/lib/courier';
import { Button, Card, Chip, EmptyState, Skeleton } from '@/ui';

/**
 * MES RELEVÉS — M1, la lecture mobile de C11 bloc 6 (D-065).
 *
 * Les trois montants du relevé, EMPILÉS : sur un téléphone, trois colonnes de
 * chiffres deviennent trois colonnes illisibles.
 *
 * « Déjà gardées » ne s'affiche que si elle existe. Sur le relevé d'un livreur
 * salarié elle vaut zéro, et une ligne à zéro pose une question au lieu d'y
 * répondre — alors que sur celui d'un livreur « normal », c'est elle qui
 * explique pourquoi le net est à zéro alors qu'il a travaillé tout le mois.
 */
function StatementsScreenContent() {
  const { t } = useI18n();
  const router = useRouter();

  /*
   * LE FILTRE VIENT DE LA FACTURATION — L2 bloc 6.
   *
   * Ses lignes « Paiements reçus » et « Paiements en attente » ouvrent cet
   * écran-ci : sans le filtre, les deux menaient à la même liste entière, et
   * un livreur qui vient de lire « 2 en attente » aurait eu à les chercher
   * parmi douze. Il reste modifiable à l'écran — on arrive avec une question,
   * on repart souvent avec une autre.
   */
  const { filter: initial } = useLocalSearchParams<{ filter?: string }>();
  const [filter, setFilter] = useState<'all' | 'paid' | 'pending'>(
    initial === 'paid' || initial === 'pending' ? initial : 'all',
  );

  const { data, isLoading, refetch } = useStatements();
  const pull = usePullToRefresh({ refetch });

  if (isLoading) {
    return (
      <SafeAreaView className="flex-1 gap-3 bg-background px-5 pt-4" edges={['top']}>
        <Skeleton className="h-32" />
        <Skeleton className="h-32" />
      </SafeAreaView>
    );
  }

  const money = (cents: number) => `${(cents / 100).toFixed(2)} MAD`;

  return (
    <SafeAreaView className="flex-1 bg-background" edges={['top']}>
      <Text className="px-5 pb-3 pt-4 text-[22px] font-bold text-foreground">
        {t('statements.title')}
      </Text>

      <View className="flex-row gap-2 px-5 pb-3">
        <Chip
          label={t('statements.all')}
          active={filter === 'all'}
          onPress={() => setFilter('all')}
        />
        <Chip
          label={t('statements.paid')}
          active={filter === 'paid'}
          onPress={() => setFilter('paid')}
        />
        <Chip
          label={t('statements.pending')}
          active={filter === 'pending'}
          onPress={() => setFilter('pending')}
        />
      </View>

      <FlatList
        keyboardShouldPersistTaps="handled"
        data={(data?.items ?? []).filter((item) =>
          filter === 'all' ? true : filter === 'paid' ? item.paidAt !== null : item.paidAt === null,
        )}
        keyExtractor={(item) => item.id}
        contentContainerClassName="gap-2 px-5 pb-6"
        refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
        ListEmptyComponent={<EmptyState icon={Wallet} title={t('statements.empty')} />}
        renderItem={({ item }) => (
          <Card className="gap-3">
            <Text className="font-mono text-[15px] text-muted-foreground">{item.code}</Text>

            <View className="gap-1.5">
              <Row label={t('statements.commissions')} value={money(item.commissionsTotal)} />

              {item.commissionsDeducted > 0 ? (
                <Row label={t('statements.kept')} value={`-${money(item.commissionsDeducted)}`} />
              ) : null}

              <View className="border-t border-border pt-1.5">
                <Row label={t('statements.due')} value={money(item.totalDue)} strong />
              </View>
            </View>

            {/*
              Le chèque ne se photographie qu'une fois le relevé RÉGLÉ : une
              photo attachée à un relevé qui n'a rien réglé ressemble à une
              preuve, et n'en est pas une (D-067).
            */}
            {item.status === 'paid' ? (
              <Button
                label={t('cheque.take')}
                icon={Camera}
                tone="ghost"
                onPress={() => router.push('/(courier)/cheque')}
                full
              />
            ) : null}
          </Card>
        )}
      />
    </SafeAreaView>
  );
}

function Row({ label, value, strong }: { label: string; value: string; strong?: boolean }) {
  return (
    <View className="flex-row items-baseline justify-between gap-3">
      <Text
        className={`text-[15px] ${strong ? 'font-semibold text-foreground' : 'text-muted-foreground'}`}
      >
        {label}
      </Text>
      <Text
        className={`tabular-nums ${strong ? 'text-[17px] font-bold text-foreground' : 'text-[15px] text-foreground'}`}
      >
        {value}
      </Text>
    </View>
  );
}

/** Coque Recette 7 (point 5) : le contenu repart vide à chaque `filter` et à chaque fermeture — `useFreshKey`. */
export default function StatementsScreen() {
  const { filter } = useLocalSearchParams<{ filter?: string }>();
  return <StatementsScreenContent key={useFreshKey(filter)} />;
}
