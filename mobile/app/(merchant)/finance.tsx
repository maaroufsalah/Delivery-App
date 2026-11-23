import type { InvoiceStatus } from '@delivery/shared';
import { INVOICE_STATUSES } from '@delivery/shared';
import { FileText, Receipt } from 'lucide-react-native';
import { useState } from 'react';
import { RefreshControl, ScrollView, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n, type MessageKey } from '@/i18n';
import { useTokens } from '@/theme/tenant-theme';
import { useInvoices, usePayouts, useSummary } from '@/lib/merchant';
import { Card, Chip, EmptyState, Field, Screen, SkeletonCard, StatusBadge } from '@/ui';

/**
 * MES FINANCES — M2 bloc 6.
 *
 * Deux listes, et la seconde n'apparaît que chez un client PROFESSIONNEL.
 *
 * Chez lui, le COD ne passe pas par la facture : il est versé à part par un bon
 * de versement, et la facture ne réclame que les frais (D-061). Chez un client
 * normal, la section serait vide en permanence — et une section vide pose une
 * question au lieu d'y répondre.
 *
 * **Pas de PDF ouvert dans l'application.** Les documents partent vers le
 * navigateur du téléphone : intégrer un lecteur PDF demanderait une dépendance
 * de plus, et le navigateur sait déjà télécharger, imprimer et partager — ce
 * qu'un comptable fera de toute façon.
 */
export default function MerchantFinanceScreen() {
  const { t } = useI18n();
  const tokens = useTokens();

  /*
   * RECHERCHE ET FILTRE — V2 vendeur bloc 5, spécification §7.
   *
   * Les deux partent au SERVEUR : `invoiceListQuerySchema` les porte déjà, et
   * trier une page de vingt-cinq lignes sur le téléphone ne trouverait jamais
   * la facture de mars.
   */
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<InvoiceStatus | null>(null);

  const summary = useSummary();
  const invoices = useInvoices({ search, status: status ?? undefined });
  const isPro = summary.data?.billingType === 'professional';
  const payouts = usePayouts(isPro);

  const pull = usePullToRefresh(invoices, payouts);

  return (
    <Screen>
      <ScrollView
        keyboardShouldPersistTaps="handled"
        contentContainerClassName="gap-3 px-5 pb-8 pt-4"
        refreshControl={
          <RefreshControl
            refreshing={pull.refreshing}
            onRefresh={() => {
              void invoices.refetch();
              if (isPro) void payouts.refetch();
            }}
          />
        }
      >
        <Text className="text-[22px] font-bold text-foreground">{t('finance.title')}</Text>

        {/* --- La recherche et les états, avant les listes --- */}
        <Field
          label={t('common.search')}
          placeholder={t('finance.searchHint')}
          value={search}
          onChangeText={setSearch}
        />

        <View className="flex-row flex-wrap gap-2">
          <Chip
            label={t('merchant.all')}
            active={status === null}
            onPress={() => setStatus(null)}
          />
          {INVOICE_STATUSES.map((value) => (
            <Chip
              key={value}
              label={t(`invoiceStatus.${value}` as MessageKey)}
              active={status === value}
              onPress={() => setStatus(status === value ? null : value)}
            />
          ))}
        </View>

        <Text className="pt-1 text-[18px] font-semibold text-foreground">
          {t('finance.invoices')}
        </Text>

        {invoices.isLoading ? (
          <SkeletonCard />
        ) : (invoices.data?.items ?? []).length === 0 ? (
          <EmptyState icon={FileText} title={t('finance.noInvoices')} />
        ) : (
          (invoices.data?.items ?? []).map((invoice) => {
            const settled = invoice.totalPaid >= invoice.totalDue;

            return (
              <Card key={invoice.id} className="gap-2">
                <View className="flex-row items-start justify-between gap-3">
                  <Text className="flex-1 font-mono text-[15px] text-muted-foreground">
                    {invoice.code}
                  </Text>
                  {settled ? (
                    <StatusBadge label={t('finance.paid')} color={tokens.success} />
                  ) : null}
                </View>

                <View className="flex-row items-baseline justify-between gap-3">
                  <Text className="text-[15px] text-muted-foreground">
                    {settled ? t('finance.paid') : t('finance.due')}
                  </Text>
                  <Text className="text-[18px] font-bold tabular-nums text-foreground">
                    {money(invoice.totalDue - invoice.totalPaid)}
                  </Text>
                </View>
              </Card>
            );
          })
        )}

        {/* --- Bons de versement : régime professionnel seulement --- */}
        {isPro ? (
          <>
            <Text className="pt-3 text-[18px] font-semibold text-foreground">
              {t('finance.payouts')}
            </Text>

            {payouts.isLoading ? (
              <SkeletonCard />
            ) : (
              (payouts.data?.items ?? []).map((payout) => (
                <Card key={payout.id} className="gap-1">
                  <Text className="font-mono text-[15px] text-muted-foreground">{payout.code}</Text>
                  <View className="flex-row items-baseline justify-between gap-3">
                    <Text className="text-[15px] text-muted-foreground">{payout.parcelCount}</Text>
                    <Text className="text-[18px] font-bold tabular-nums text-foreground">
                      {money(payout.amount)}
                    </Text>
                  </View>
                </Card>
              ))
            )}

            {(payouts.data?.items ?? []).length === 0 && !payouts.isLoading ? (
              <EmptyState icon={Receipt} title={t('finance.noInvoices')} />
            ) : null}
          </>
        ) : null}
      </ScrollView>
    </Screen>
  );
}

/** Les montants voyagent en CENTIMES : aucun flottant n'entre dans le calcul. */
function money(cents: number): string {
  return `${(cents / 100).toFixed(2)} MAD`;
}
