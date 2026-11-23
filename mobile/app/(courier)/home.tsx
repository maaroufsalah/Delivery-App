import { courierFigures } from '@delivery/shared';
import { useRouter } from 'expo-router';
import {
  Banknote,
  Bell,
  Boxes,
  ChevronRight,
  type LucideIcon,
  PackageSearch,
  RotateCcw,
  TrendingUp,
  Truck,
  Wallet,
  Warehouse,
} from 'lucide-react-native';
import { useState } from 'react';
import { Pressable, RefreshControl, ScrollView, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n } from '@/i18n';
import { ApiError } from '@/lib/api';
import {
  useAnnouncements,
  useDeliveries,
  useMe,
  usePerformance,
  useReturnRoutes,
  useRoutes,
} from '@/lib/courier';
import { usePettyCash } from '@/lib/petty-cash';
import { useRefetchOnFocus } from '@/lib/use-refetch-on-focus';
import { useSession } from '@/lib/session';
import {
  courierSurface,
  initialsOf,
  SOFT_SHADOW,
  type TileKey,
  tilePastille,
} from '@/theme/courier-palette';
import { useTenantTheme } from '@/theme/tenant-theme';
import { NewsSheet } from '@/components/news-sheet';
import { Skeleton } from '@/ui';

/**
 * L'ACCUEIL DU LIVREUR, v2 — chantier « Accueil et menu bas livreur
 * simplifiés », maquette décrite dans `docs/design/livreur-accueil-v2.md`.
 *
 * **Ce qui change, et ce qui ne change pas.** L'accueil devient une PORTE :
 * qui je suis, trois chiffres, six tuiles vers les écrans de travail. Le
 * tableau de bord à huit indicateurs, l'anneau de performance et l'activité
 * récente le quittent — ils vivent déjà dans « Mes statistiques » (jour /
 * mois) et dans « Colis », où ils sont à un geste. Aucun écran métier ne
 * bouge, aucun appel nouveau : les trois chiffres sont ceux que l'accueil
 * chargeait déjà (`GET /courier/me/performance`, les tournées actives).
 *
 * **L'en-tête est PLAT**, couleur du tenant : la maquette interdit les
 * dégradés, et `GradientHeader` n'a plus sa place ici. L'accent vient de
 * `useTokens().primary`, les neutres de `courierSurface()` — rien n'est écrit
 * pour une marque (CLAUDE.md, règle 3).
 *
 * **Les trois chiffres** : « À livrer aujourd'hui » = ce qu'il reste à livrer
 * sur les tournées du jour ; « Ramassages à faire » = les colis à ramasser
 * (repli sur les tournées de ramassage actives quand l'API de performance ne
 * répond pas) ; « MAD en main » = ce que le livreur porte et devra remettre
 * (`toRemitInCents`, repli sur l'encaissé du jour). Une valeur inconnue
 * affiche « — », jamais un zéro qui rassurerait à tort.
 */
export default function CourierHome() {
  const { t, locale } = useI18n();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const { profile } = useSession();
  const { brand, tokens, scheme } = useTenantTheme();
  const surface = courierSurface(scheme, tokens);

  const me = useMe();
  const active = useRoutes('active');
  const news = useAnnouncements(2);
  const performance = usePerformance('day');
  // D-168 — ses brouillons de petite caisse : le badge de la tuile.
  const pettyCash = usePettyCash();
  // D-179 — ses circuits de retour : le badge de la tuile Retours.
  const returnRoutes = useReturnRoutes();
  /*
   * Le STOCK — tous ses colis à livrer, circuits partis confondus : la liste
   * que rend déjà `GET /courier/me/deliveries`. La pastille de la tuile compte
   * ses lignes ; aucun appel nouveau côté serveur.
   */
  const deliveries = useDeliveries();
  const pull = usePullToRefresh(active, news, performance, me, pettyCash, returnRoutes, deliveries);
  // Tuiles et compteurs relus au retour : un circuit clos ne compte plus.
  useRefetchOnFocus(active, performance, pettyCash, returnRoutes, deliveries);
  // Inconnu tant que la liste n'est pas lue : pas de pastille plutôt qu'un zéro.
  const stockCount = deliveries.data ? deliveries.data.parcels.length : null;
  // Les colis qui restent à rendre compte sur ses circuits de retour.
  const returnsToDo = returnRoutes.data
    ? returnRoutes.data.reduce((sum, route) => sum + route.remainingCount, 0)
    : null;
  // Sans la clé `pettycash.courier` l'API répond 403 : la tuile disparaît.
  const pettyCashClosed = pettyCash.error instanceof ApiError && pettyCash.error.status === 403;

  const [newsOpen, setNewsOpen] = useState(false);

  const figures = courierFigures(performance.data, active.data);
  const loading = active.isLoading && performance.isLoading;

  /*
   * Repli pour les ramassages : sans `GET /courier/me/performance`, les
   * tournées de ramassage actives disent encore combien de colis attendent.
   */
  const pickupsFromRoutes = (active.data ?? [])
    .filter((route) => route.kind === 'pickup')
    .reduce((sum, route) => sum + route.remainingCount, 0);
  const pickupsToDo = figures.toPickup ?? (active.data ? pickupsFromRoutes : null);
  const cashInHand = figures.toRemitInCents ?? figures.collectedInCents;

  const money = (cents: number) =>
    new Intl.NumberFormat(locale === 'ar' ? 'ar-MA' : 'fr-MA', {
      maximumFractionDigits: 0,
    }).format(cents / 100);

  const fullName = profile ? `${profile.firstName} ${profile.lastName}` : (brand?.name ?? '');
  const subtitle = me.data?.agencyName
    ? `${t('home.role')} · ${me.data.agencyName}`
    : t('home.role');

  const tiles: {
    key: TileKey | 'pettyCash';
    icon: LucideIcon;
    title: string;
    hint: string;
    badge?: number | null;
    href: string;
    /** D-168 — la couleur du TENANT, pas une pastille de la palette fixe. */
    pastille?: { tint: string; icon: string };
  }[] = [
    {
      key: 'pickups',
      icon: Warehouse,
      title: t('home.tile.pickups'),
      hint: t('home.tile.pickupsHint'),
      badge: pickupsToDo,
      href: '/(courier)/pickups',
    },
    {
      key: 'deliveries',
      icon: Truck,
      title: t('home.tile.deliveries'),
      hint: t('home.tile.deliveriesHint'),
      badge: figures.toDeliver,
      href: '/(courier)/circuits',
    },
    // « Stock » : la liste unique de D-242, sortie du menu Livraisons.
    {
      key: 'stock',
      icon: Boxes,
      title: t('home.tile.stock'),
      hint: t('home.tile.stockHint'),
      badge: stockCount,
      href: '/(courier)/stock',
    },
    {
      key: 'parcels',
      icon: PackageSearch,
      title: t('home.tile.parcels'),
      hint: t('home.tile.parcelsHint'),
      href: '/(courier)/parcels',
    },
    {
      key: 'returns',
      icon: RotateCcw,
      title: t('home.tile.returns'),
      hint: t('home.tile.returnsHint'),
      badge: returnsToDo,
      href: '/(courier)/returns',
    },
    {
      key: 'remittances',
      icon: Banknote,
      title: t('home.tile.remittances'),
      hint: t('home.tile.remittancesHint'),
      href: '/(courier)/remittances',
    },
    ...(pettyCashClosed
      ? []
      : [
          {
            key: 'pettyCash' as const,
            icon: Wallet,
            title: t('home.tile.pettyCash'),
            hint: t('home.tile.pettyCashHint'),
            badge: pettyCash.data?.drafts ?? null,
            href: '/(courier)/petty-cash',
            pastille: { tint: tokens.accent, icon: tokens.primary },
          },
        ]),
    {
      key: 'stats',
      icon: TrendingUp,
      title: t('home.tile.stats'),
      hint: t('home.tile.statsHint'),
      href: '/(courier)/stats',
    },
  ];

  return (
    <View className="flex-1" style={{ backgroundColor: surface.background }}>
      <ScrollView
        keyboardShouldPersistTaps="handled"
        contentContainerClassName="pb-8"
        refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
      >
        {/* --- L'en-tête, couleur du tenant, à plat --- */}
        <View
          style={{
            backgroundColor: tokens.primary,
            paddingTop: insets.top + 12,
            paddingBottom: 56,
          }}
          className="px-5"
        >
          <View className="flex-row items-center gap-3">
            <View
              accessibilityElementsHidden
              className="h-12 w-12 items-center justify-center rounded-full"
              style={{ backgroundColor: 'rgba(255,255,255,0.22)' }}
            >
              <Text
                className="text-[18px] font-bold"
                style={{ color: tokens['primary-foreground'] }}
              >
                {initialsOf(profile?.firstName, profile?.lastName)}
              </Text>
            </View>

            <View className="flex-1 gap-0.5">
              <Text
                className="text-[20px] font-bold"
                style={{ color: tokens['primary-foreground'] }}
                numberOfLines={1}
              >
                {fullName}
              </Text>
              <Text
                className="text-[14px]"
                style={{ color: tokens['primary-foreground'], opacity: 0.85 }}
                numberOfLines={1}
              >
                {subtitle}
              </Text>
            </View>

            {/*
              La cloche : le point ne s'allume que s'il y a quelque chose à
              lire — une pastille permanente cesse d'être un signal au bout de
              deux jours.
            */}
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={t('home.notifications')}
              onPress={() => setNewsOpen(true)}
              className="min-h-11 min-w-11 items-center justify-center rounded-full"
              hitSlop={8}
              style={{ backgroundColor: 'rgba(255,255,255,0.18)' }}
            >
              <Bell size={22} color={tokens['primary-foreground']} />
              {news.data && news.data.length > 0 ? (
                <View
                  className="absolute end-2.5 top-2.5 h-2.5 w-2.5 rounded-full"
                  style={{ backgroundColor: tokens.warning }}
                />
              ) : null}
            </Pressable>
          </View>
        </View>

        {/* --- Le bandeau des trois chiffres, à cheval sur l'en-tête --- */}
        <View className="-mt-10 px-5">
          <View
            className="flex-row rounded-[20px] px-2 py-4"
            style={[{ backgroundColor: surface.card }, SOFT_SHADOW]}
          >
            <Figure
              label={t('home.figures.toDeliverToday')}
              value={loading ? null : String(figures.toDeliver)}
              color={surface.text}
              muted={surface.muted}
            />
            <View className="w-px self-stretch" style={{ backgroundColor: surface.border }} />
            <Figure
              label={t('home.figures.pickupsToDo')}
              value={loading ? null : pickupsToDo === null ? '—' : String(pickupsToDo)}
              color={surface.text}
              muted={surface.muted}
            />
            <View className="w-px self-stretch" style={{ backgroundColor: surface.border }} />
            <Figure
              label={t('home.figures.cashInHand')}
              value={loading ? null : money(cashInHand)}
              color={surface.text}
              muted={surface.muted}
            />
          </View>
        </View>

        {/* --- Six portes --- */}
        <View className="gap-3 px-5 pt-6">
          <Text className="text-[18px] font-semibold" style={{ color: surface.text }}>
            {t('home.ask')}
          </Text>

          <View className="flex-row flex-wrap justify-between">
            {tiles.map((tile) => {
              const pastille = tile.pastille ?? tilePastille(tile.key as TileKey, scheme);
              const Icon = tile.icon;

              return (
                <Pressable
                  key={tile.key}
                  accessibilityRole="button"
                  accessibilityLabel={tile.title}
                  accessibilityHint={tile.hint}
                  onPress={() => router.push(tile.href as never)}
                  className="mb-3 w-[48%] rounded-[20px] p-4"
                  style={({ pressed }) => [
                    { backgroundColor: surface.card, minHeight: 148 },
                    SOFT_SHADOW,
                    pressed ? { opacity: 0.85 } : null,
                  ]}
                >
                  <View className="flex-row items-start justify-between">
                    <View
                      className="h-12 w-12 items-center justify-center rounded-2xl"
                      style={{ backgroundColor: pastille.tint }}
                    >
                      <Icon size={26} color={pastille.icon} />
                    </View>
                    {tile.badge ? (
                      <View
                        className="min-w-[24px] items-center justify-center rounded-full px-1.5 py-0.5"
                        style={{ backgroundColor: tokens.primary }}
                      >
                        <Text
                          className="text-[12px] font-bold"
                          style={{ color: tokens['primary-foreground'] }}
                        >
                          {tile.badge > 99 ? '99+' : tile.badge}
                        </Text>
                      </View>
                    ) : null}
                  </View>

                  <View className="mt-3 gap-0.5">
                    <Text
                      className="text-[16px] font-semibold"
                      style={{ color: surface.text }}
                      numberOfLines={1}
                    >
                      {tile.title}
                    </Text>
                    <Text
                      className="text-[13px]"
                      style={{ color: surface.muted }}
                      numberOfLines={2}
                    >
                      {tile.hint}
                    </Text>
                  </View>

                  <ChevronRight
                    size={16}
                    color={surface.muted}
                    className="flip-rtl absolute bottom-4 end-4"
                  />
                </Pressable>
              );
            })}
          </View>
        </View>
      </ScrollView>

      <NewsSheet open={newsOpen} onClose={() => setNewsOpen(false)} />
    </View>
  );
}

/** Un chiffre du bandeau : la valeur en grand, le libellé dessous, en deux lignes au plus. */
function Figure({
  label,
  value,
  color,
  muted,
}: {
  label: string;
  /** `null` tant que ça charge : un squelette plutôt qu'un zéro. */
  value: string | null;
  color: string;
  muted: string;
}) {
  return (
    <View className="flex-1 items-center gap-1 px-1">
      {value === null ? (
        <Skeleton className="h-7 w-12" />
      ) : (
        <Text className="text-[22px] font-bold tabular-nums" style={{ color }} numberOfLines={1}>
          {value}
        </Text>
      )}
      <Text className="text-center text-[12px]" style={{ color: muted }} numberOfLines={2}>
        {label}
      </Text>
    </View>
  );
}
