import {
  AlertTriangle,
  Boxes,
  FileText,
  Megaphone,
  Menu,
  MessageSquare,
  PackagePlus,
  TrendingUp,
  Truck,
} from 'lucide-react-native';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Pressable, RefreshControl, ScrollView, Text, View } from 'react-native';
import { getParcelStatus, MERCHANT_PARCEL_BUCKET_KEYS } from '@delivery/shared';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n, type MessageKey } from '@/i18n';
import { useNews, useSummary } from '@/lib/merchant';
import { useSession } from '@/lib/session';
import { RoleMenu } from '@/components/role-menu';
import {
  Button,
  Card,
  Divider,
  EmptyState,
  GradientHeader,
  KpiRow,
  KpiTile,
  Screen,
  SkeletonCard,
} from '@/ui';

/**
 * L'ACCUEIL de l'e-commerçant — M2.
 *
 * Quatre chiffres et trois actualités. C'est un écran qu'on ouvre debout, entre
 * deux commandes, pour une seule question : qu'est-ce qui s'est passé
 * aujourd'hui, et combien me doit-on. Le détail vit dans les écrans qui ont le
 * temps de l'expliquer.
 *
 * **Le montant dû est mis en avant, seul sur sa carte.** C'est la question qui
 * amène un vendeur ici ; la noyer dans une grille de quatre cases identiques
 * l'obligerait à la chercher chaque fois.
 */
export default function MerchantHomeScreen() {
  const { t, status } = useI18n();
  const { profile } = useSession();
  const [menu, setMenu] = useState(false);
  const router = useRouter();
  const summary = useSummary();
  const news = useNews();

  const pull = usePullToRefresh(summary, news);

  /* Le libellé vient du référentiel partagé, comme sur la fiche du colis :
     deux tables de statuts finiraient par se contredire au même écran. */
  const statusLabel = (statusId: number): string => {
    const definition = getParcelStatus(statusId);
    return definition ? status(definition.key) : '';
  };

  if (summary.isLoading) {
    return (
      <Screen className="gap-3 px-5 pt-4">
        <SkeletonCard />
        <SkeletonCard />
        <SkeletonCard />
      </Screen>
    );
  }

  const data = summary.data;

  return (
    <View className="flex-1 bg-background">
      <ScrollView
        keyboardShouldPersistTaps="handled"
        contentContainerClassName="pb-8"
        refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
      >
        {/* --- L'en-tête de marque, comme chez le livreur — M4 bloc 5 --- */}
        <GradientHeader height={120}>
          <View className="flex-row items-center justify-between">
            <Text className="flex-1 text-[24px] font-bold text-white" numberOfLines={1}>
              {t('merchant.hello', { name: profile?.firstName ?? '' })}
            </Text>
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={t('menu.title')}
              onPress={() => setMenu(true)}
              className="min-h-11 min-w-11 items-center justify-center rounded-full bg-white/15"
              hitSlop={8}
            >
              <Menu size={22} color="#FFFFFF" />
            </Pressable>
          </View>
        </GradientHeader>

        <View className="gap-3 px-5 pt-4">
          {/* --- Ce qu'on nous doit : la question du matin --- */}
          <Card className="gap-1">
            <Text className="text-[15px] text-muted-foreground">{t('merchant.codToReceive')}</Text>
            <Text className="text-[32px] font-bold tabular-nums text-foreground">
              {money(data?.codToReceive ?? 0)}
            </Text>
            <Text className="text-[13px] text-muted-foreground">
              {/*
              Le régime change la phrase. Chez un client « professionnel », le
              COD part par bon de versement et la facture ne réclame que les
              frais (D-061) : lui servir la phrase du régime normal lui ferait
              attendre un virement qui ne viendra pas de là.
            */}
              {data?.billingType === 'professional'
                ? t('merchant.codHintPro')
                : t('merchant.codHint')}
            </Text>
          </Card>

          {/*
          LA JOURNÉE, en tuiles du système — M5.

          `KpiTile` est celle de l'accueil livreur : le chiffre d'abord, le
          libellé dessous, parce que c'est ce qu'on lit à un mètre. « En
          transit » rejoint les deux autres au lieu de garder sa carte à part —
          il répond à la même question qu'elles.
        */}
          <KpiRow>
            <KpiTile
              label={t('merchant.createdToday')}
              value={String(data?.createdToday ?? 0)}
              icon={PackagePlus}
              tone="primary"
            />
            <KpiTile
              label={t('merchant.deliveredToday')}
              value={String(data?.deliveredToday ?? 0)}
              icon={TrendingUp}
              tone="success"
            />
            <KpiTile
              label={t('merchant.inTransit')}
              value={String(data?.inTransit ?? 0)}
              icon={Truck}
            />
          </KpiRow>

          {/*
            LES HUIT SEAUX DE LA SPÉCIFICATION §8 — V2 vendeur bloc 2.

            Ils répondent à une autre question que les trois tuiles du dessus :
            celles-là disent « aujourd'hui », ceux-ci disent « où en est mon
            activité ». Les mélanger aurait fait une grille de onze cases dont
            personne n'aurait su laquelle lire.

            **Les seaux viennent du paquet partagé**, pas d'une liste écrite
            ici : le serveur les remplit avec la MÊME correspondance, et un
            statut ajouté demain tombera du bon côté des deux côtés à la fois.
          */}
          <Text className="pt-2 text-[18px] font-semibold text-foreground">
            {t('merchant.parcelsTitle', { count: data?.parcelsTotal ?? 0 })}
          </Text>

          <View className="flex-row flex-wrap gap-2">
            {MERCHANT_PARCEL_BUCKET_KEYS.map((bucket) => (
              <View
                key={bucket}
                className="min-w-[30%] flex-1 gap-0.5 rounded-2xl border border-border bg-card px-3 py-2"
              >
                <Text className="text-[20px] font-bold tabular-nums text-foreground">
                  {data?.parcels[bucket] ?? 0}
                </Text>
                <Text className="text-[13px] text-muted-foreground" numberOfLines={1}>
                  {t(`merchantBucket.${bucket}` as MessageKey)}
                </Text>
              </View>
            ))}
          </View>

          {/*
            CE QUI APPELLE UN GESTE, et seulement ça. Trois lignes qui ne
            s'affichent que lorsqu'elles ont quelque chose à dire : un « 0
            facture impayée » occupe la place sans rien apprendre.
          */}
          {(data?.unpaidInvoices ?? 0) > 0 ||
          (data?.openClaims ?? 0) > 0 ||
          (data?.lowStockLines ?? 0) > 0 ? (
            <Card className="gap-1 p-1">
              {(data?.unpaidInvoices ?? 0) > 0 ? (
                <Alert
                  icon={FileText}
                  label={t('merchant.unpaidInvoices', { count: data?.unpaidInvoices ?? 0 })}
                  hint={money(data?.unpaidAmount ?? 0)}
                  onPress={() => router.push('/(merchant)/finance')}
                />
              ) : null}
              {(data?.openClaims ?? 0) > 0 ? (
                <>
                  {(data?.unpaidInvoices ?? 0) > 0 ? <Divider /> : null}
                  <Alert
                    icon={MessageSquare}
                    label={t('merchant.openClaims', { count: data?.openClaims ?? 0 })}
                    onPress={() => router.push('/(merchant)/claims')}
                  />
                </>
              ) : null}
              {(data?.lowStockLines ?? 0) > 0 ? (
                <>
                  {(data?.unpaidInvoices ?? 0) > 0 || (data?.openClaims ?? 0) > 0 ? (
                    <Divider />
                  ) : null}
                  <Alert
                    icon={Boxes}
                    label={t('merchant.lowStockLines', { count: data?.lowStockLines ?? 0 })}
                    onPress={() => router.push('/(merchant)/warehouse')}
                  />
                </>
              ) : null}
            </Card>
          ) : null}

          {/* --- Dernières activités — spécification §2 --- */}
          <Text className="pt-2 text-[18px] font-semibold text-foreground">
            {t('merchant.activity')}
          </Text>

          {(data?.activity ?? []).length === 0 ? (
            <EmptyState icon={AlertTriangle} title={t('merchant.noActivity')} />
          ) : (
            <Card className="gap-1 p-1">
              {(data?.activity ?? []).map((event, index) => (
                <View key={`${event.parcelCode ?? 'x'}-${event.at}-${index}`}>
                  {index > 0 ? <Divider /> : null}
                  <Pressable
                    accessibilityRole="button"
                    disabled={!event.parcelCode}
                    onPress={() =>
                      event.parcelCode
                        ? router.push(`/(merchant)/parcel/${encodeURIComponent(event.parcelCode)}`)
                        : undefined
                    }
                    className="min-h-[52px] justify-center gap-0.5 rounded-2xl px-3"
                    style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
                  >
                    <Text className="text-[15px] text-foreground" numberOfLines={1}>
                      {statusLabel(event.statusId)}
                    </Text>
                    <Text className="font-mono text-[13px] text-muted-foreground">
                      {event.parcelCode ?? ''}
                    </Text>
                  </Pressable>
                </View>
              ))}
            </Card>
          )}

          <Button
            label={t('claims.title')}
            icon={MessageSquare}
            tone="ghost"
            onPress={() => router.push('/(merchant)/claims')}
            full
          />

          {/* --- Actualités de la société --- */}
          <Text className="pt-2 text-[18px] font-semibold text-foreground">
            {t('merchant.news')}
          </Text>

          {news.isLoading ? (
            <SkeletonCard />
          ) : (news.data ?? []).length === 0 ? (
            <EmptyState icon={Megaphone} title={t('merchant.noNews')} />
          ) : (
            (news.data ?? []).map((item) => (
              <Card key={item.id} className="gap-1">
                <Text className="text-[16px] font-semibold text-foreground">{item.title}</Text>
                {item.intro ? (
                  <Text className="text-[15px] text-muted-foreground">{item.intro}</Text>
                ) : null}
              </Card>
            ))
          )}
        </View>
      </ScrollView>

      <RoleMenu open={menu} onClose={() => setMenu(false)} space="merchant" />
    </View>
  );
}

/** Une ligne d'alerte : elle n'apparaît que lorsqu'elle a quelque chose à dire. */
function Alert({
  icon: Icon,
  label,
  hint,
  onPress,
}: {
  icon: typeof MessageSquare;
  label: string;
  hint?: string;
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
      {hint ? <Text className="text-[15px] tabular-nums text-muted-foreground">{hint}</Text> : null}
    </Pressable>
  );
}

/** Les montants voyagent en CENTIMES : aucun flottant n'entre dans le calcul. */
function money(cents: number): string {
  return `${(cents / 100).toFixed(2)} MAD`;
}
