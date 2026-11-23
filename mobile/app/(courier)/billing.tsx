import { useRouter } from 'expo-router';
import {
  Banknote,
  ChevronRight,
  Clock,
  FileText,
  History,
  Percent,
  Scale,
  Wallet,
} from 'lucide-react-native';
import type { LucideIcon } from 'lucide-react-native';
import { Pressable, RefreshControl, ScrollView, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n } from '@/i18n';
import { useCourierBalance, usePerformance, useRemittances, useStatements } from '@/lib/courier';
import { Card, Divider, KpiRow, KpiTile, Screen, SectionTitle, Skeleton } from '@/ui';

/**
 * LA FACTURATION — L2 bloc 6, maquette 10.
 *
 * **Aucun endpoint neuf.** `GET /remittances` et `GET /courier-statements`
 * tournent depuis M1 et la portée serveur les borne au livreur. Ce qui manquait
 * n'était pas la donnée, c'était l'écran qui la rassemble : le livreur avait
 * deux listes — ses remises, ses relevés — et aucun endroit d'où lire son
 * mois d'un coup d'œil.
 *
 * **Trois des quatre tuiles d'Anass, et une quatrième qui change.** Il demande
 * Total encaissé, Frais de livraison, Commissions, À remettre. Les frais de
 * livraison ne sont pas l'argent du livreur : c'est ce que la société facture à
 * l'e-commerçant, et D-072 dit que la donnée commerciale ne voyage pas sur un
 * téléphone qui se perd. La tuile devient **« Net à payer »** — ce que l'agence
 * lui doit — qui est le chiffre qu'il cherche vraiment à cet endroit (D-L2-9).
 *
 * **« À remettre » vient de l'endpoint de performance**, non déployé : la tuile
 * disparaît tant que la production ne le sert pas, plutôt que d'afficher un
 * zéro qui voudrait dire « vous ne devez rien ».
 */
export default function CourierBilling() {
  const { t } = useI18n();
  const router = useRouter();

  const statements = useStatements();
  const remittances = useRemittances();
  const performance = usePerformance('month');
  /* Son solde — chantier « Finance », point 4. */
  const balance = useCourierBalance();
  const pull = usePullToRefresh(statements, remittances, performance);

  const rows = statements.data?.items ?? [];
  const loading = statements.isLoading || remittances.isLoading;

  /*
   * Les totaux se somment sur les RELEVÉS, pas sur les remises : un relevé est
   * la pièce qui fait foi entre l'agence et le livreur (C11, D-065), et les
   * remises n'en portent qu'une partie — le cash rendu, pas les commissions.
   */
  const collected = rows.reduce((sum, row) => sum + row.codCollected, 0);
  const commissions = rows.reduce((sum, row) => sum + row.commissionsTotal, 0);
  const due = rows.filter((row) => row.paidAt === null).reduce((sum, row) => sum + row.totalDue, 0);

  const paidCount = rows.filter((row) => row.paidAt !== null).length;
  const pendingCount = rows.length - paidCount;

  const money = (cents: number) => `${(cents / 100).toFixed(2)} MAD`;

  return (
    <Screen>
      <Text className="px-5 pb-3 pt-4 text-[22px] font-bold text-foreground">
        {t('billing.title')}
      </Text>

      <ScrollView
        keyboardShouldPersistTaps="handled"
        contentContainerClassName="gap-5 px-5 pb-8"
        refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
      >
        {loading ? (
          <View className="gap-3">
            <Skeleton className="h-[86px]" />
            <Skeleton className="h-[86px]" />
          </View>
        ) : (
          <View className="gap-3">
            <KpiRow>
              <KpiTile
                label={t('billing.collected')}
                value={money(collected)}
                icon={Banknote}
                tone="success"
              />
              <KpiTile
                label={t('billing.commissions')}
                value={money(commissions)}
                icon={Percent}
                tone="primary"
              />
            </KpiRow>

            <KpiRow>
              <KpiTile label={t('billing.due')} value={money(due)} icon={Wallet} />
              {performance.data ? (
                <KpiTile
                  label={t('home.toRemit')}
                  value={money(performance.data.toRemitInCents)}
                  icon={Clock}
                  tone="warning"
                />
              ) : null}
            </KpiRow>

            {/*
              SON SOLDE — chantier « Finance », point 4, et seulement s'il
              n'est pas nul. Une tuile à « 0,00 MAD » sur la moitié des
              livreurs aurait dilué celle qui compte : celle du livreur qui
              doit deux cents dirhams et ne le savait pas.
            */}
            {balance.data && balance.data.balance !== 0 ? (
              <KpiRow>
                <KpiTile
                  label={t('billing.balance')}
                  value={money(balance.data.balance)}
                  icon={Scale}
                  tone={balance.data.balance < 0 ? 'warning' : 'success'}
                />
              </KpiRow>
            ) : null}
          </View>
        )}

        {/* --- Les quatre lignes de détail de la maquette --- */}
        <View className="gap-3">
          <SectionTitle label={t('billing.details')} />

          <Card className="gap-1 p-1">
            <DetailRow
              icon={Banknote}
              label={t('billing.paymentsReceived')}
              value={String(paidCount)}
              onPress={() => router.push('/(courier)/statements?filter=paid')}
            />
            <Divider />
            <DetailRow
              icon={Clock}
              label={t('billing.paymentsPending')}
              value={String(pendingCount)}
              onPress={() => router.push('/(courier)/statements?filter=pending')}
            />
            <Divider />
            <DetailRow
              icon={FileText}
              label={t('billing.statements')}
              value={String(rows.length)}
              onPress={() => router.push('/(courier)/statements')}
            />
            <Divider />
            <DetailRow
              icon={History}
              label={t('billing.history')}
              value={String(remittances.data?.items.length ?? 0)}
              onPress={() => router.push('/(courier)/remittances')}
            />
          </Card>
        </View>
      </ScrollView>
    </Screen>
  );
}

/** Une ligne « Voir détails » : le libellé, son compte, un chevron. */
function DetailRow({
  icon: Icon,
  label,
  value,
  onPress,
}: {
  icon: LucideIcon;
  label: string;
  value: string;
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
      <Text className="text-[15px] font-semibold tabular-nums text-muted-foreground">{value}</Text>
      <ChevronRight size={18} className="flip-rtl text-muted-foreground" />
    </Pressable>
  );
}
