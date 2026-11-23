import { CLAIM_CLOSED_STATUSES } from '@delivery/shared';
import Constants from 'expo-constants';
import { useRouter } from 'expo-router';
import {
  Banknote,
  ChevronRight,
  Coins,
  FileSignature,
  ListChecks,
  LogOut,
  type LucideIcon,
  MessageSquare,
  Newspaper,
  Settings,
  TrendingUp,
  Wallet,
} from 'lucide-react-native';
import { useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { useClaims } from '@/lib/claims';
import { useMe } from '@/lib/courier';
import { useSession } from '@/lib/session';
import { courierSurface, initialsOf, SOFT_SHADOW } from '@/theme/courier-palette';
import { useTenantTheme } from '@/theme/tenant-theme';
import { NewsSheet } from '@/components/news-sheet';
import { Screen } from '@/ui';

/**
 * « PLUS », v2 — chantier « Accueil et menu bas livreur simplifiés »,
 * maquette décrite dans `docs/design/livreur-accueil-v2.md`.
 *
 * **Une carte de profil en tête**, puis deux sections : « Activité » (ce que
 * je consulte) et « Application » (ce que je règle). Les écrans de TRAVAIL —
 * Retours, Retour de fonds — ne sont plus ici : ils sont des tuiles de
 * l'accueil, à un geste. Les Bons BL et les Relevés restent atteignables :
 * les premiers n'ont pas d'autre porte, les seconds sont liés depuis
 * Facturation mais gardent leur ligne, parce qu'une entrée qu'on cherche et
 * qu'on ne trouve plus est une régression même quand un autre chemin existe.
 *
 * **Actualités entre ici** : la feuille que la cloche de l'accueil ouvre, et
 * qui n'avait pas de seconde porte. **Se déconnecter reste** — le spec
 * `logout.spec.ts` le tient.
 *
 * Aucun écran métier ne change, aucune route ne change ; le badge des
 * réclamations lit la liste que l'écran Réclamations charge déjà.
 */
export default function CourierMore() {
  const { t } = useI18n();
  const router = useRouter();
  const { signOut, profile } = useSession();
  const { brand, tokens, scheme } = useTenantTheme();
  const surface = courierSurface(scheme, tokens);

  const me = useMe();
  const claims = useClaims();
  const [newsOpen, setNewsOpen] = useState(false);

  const openClaims = (claims.data?.items ?? []).filter(
    (claim) => !CLAIM_CLOSED_STATUSES.includes(claim.status),
  ).length;

  const fullName = profile ? `${profile.firstName} ${profile.lastName}` : (brand?.name ?? '');
  const where = [t('home.role'), me.data?.cityName].filter(Boolean).join(' · ');

  return (
    <Screen>
      <View className="flex-1" style={{ backgroundColor: surface.background }}>
        <Text className="px-5 pb-3 pt-4 text-[22px] font-bold" style={{ color: surface.text }}>
          {t('more.title')}
        </Text>

        <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-5 px-5 pb-8">
          {/* --- Moi : la carte de profil --- */}
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={t('menu.profile')}
            onPress={() => router.push('/(courier)/profile')}
            className="flex-row items-center gap-3 rounded-[20px] p-4"
            style={({ pressed }) => [
              { backgroundColor: surface.card },
              SOFT_SHADOW,
              pressed ? { opacity: 0.85 } : null,
            ]}
          >
            <View
              className="h-14 w-14 items-center justify-center rounded-full"
              style={{ backgroundColor: tokens.primary }}
            >
              <Text
                className="text-[20px] font-bold"
                style={{ color: tokens['primary-foreground'] }}
              >
                {initialsOf(profile?.firstName, profile?.lastName)}
              </Text>
            </View>
            <View className="flex-1 gap-0.5">
              <Text
                className="text-[17px] font-semibold"
                style={{ color: surface.text }}
                numberOfLines={1}
              >
                {fullName}
              </Text>
              <Text className="text-[14px]" style={{ color: surface.muted }} numberOfLines={1}>
                {where}
              </Text>
              <Text className="text-[13px] font-medium" style={{ color: tokens.primary }}>
                {t('more.profileHint')}
              </Text>
            </View>
            <ChevronRight size={20} color={surface.muted} className="flip-rtl" />
          </Pressable>

          {/* --- Activité --- */}
          <Section title={t('more.activity')} color={surface.muted}>
            <Group card={surface.card} border={surface.border}>
              <Row
                icon={TrendingUp}
                label={t('stats.title')}
                onPress={() => router.push('/(courier)/stats')}
                surface={surface}
                accent={tokens.primary}
              />
              <Row
                icon={Banknote}
                label={t('billing.title')}
                onPress={() => router.push('/(courier)/billing')}
                surface={surface}
                accent={tokens.primary}
              />
              {/* D-168 — la petite caisse, aussi en tuile d'accueil. */}
              <Row
                icon={Coins}
                label={t('pettyCash.title')}
                onPress={() => router.push('/(courier)/petty-cash')}
                surface={surface}
                accent={tokens.primary}
              />
              <Row
                icon={Wallet}
                label={t('statements.title')}
                onPress={() => router.push('/(courier)/statements')}
                surface={surface}
                accent={tokens.primary}
              />
              <Row
                icon={FileSignature}
                label={t('bl.notes')}
                onPress={() => router.push('/(courier)/bl')}
                surface={surface}
                accent={tokens.primary}
              />
              <Row
                icon={ListChecks}
                label={t('more.statusesShort')}
                onPress={() => router.push('/(courier)/statuses')}
                surface={surface}
                accent={tokens.primary}
              />
              <Row
                icon={MessageSquare}
                label={t('claims.title')}
                badge={openClaims}
                onPress={() => router.push('/(courier)/claims')}
                surface={surface}
                accent={tokens.primary}
                badgeColor={tokens.destructive}
                badgeText={tokens['destructive-foreground']}
                last
              />
            </Group>
          </Section>

          {/* --- Application --- */}
          <Section title={t('more.application')} color={surface.muted}>
            <Group card={surface.card} border={surface.border}>
              <Row
                icon={Newspaper}
                label={t('menu.news')}
                onPress={() => setNewsOpen(true)}
                surface={surface}
                accent={tokens.primary}
              />
              <Row
                icon={Settings}
                label={t('settings.title')}
                onPress={() => router.push('/(courier)/settings')}
                surface={surface}
                accent={tokens.primary}
              />
              <Row
                icon={LogOut}
                label={t('profile.logout')}
                onPress={() => void signOut()}
                surface={surface}
                accent={tokens.destructive}
                danger
                last
              />
            </Group>
          </Section>

          {/*
            Le pied porte la société et la version : c'est ce qu'on demande à
            un livreur au téléphone quand quelque chose ne va pas.
          */}
          <View className="items-center gap-0.5 pt-1">
            <Text className="text-[13px]" style={{ color: surface.muted }}>
              {brand?.name ?? ''}
            </Text>
            <Text className="text-[12px]" style={{ color: surface.muted }}>
              {t('menu.version', { version: Constants.expoConfig?.version ?? '—' })}
            </Text>
          </View>
        </ScrollView>
      </View>

      <NewsSheet open={newsOpen} onClose={() => setNewsOpen(false)} />
    </Screen>
  );
}

function Section({
  title,
  color,
  children,
}: {
  title: string;
  color: string;
  children: React.ReactNode;
}) {
  return (
    <View className="gap-2">
      <Text className="px-1 text-[13px] font-semibold uppercase tracking-wide" style={{ color }}>
        {title}
      </Text>
      {children}
    </View>
  );
}

function Group({
  card,
  border,
  children,
}: {
  card: string;
  border: string;
  children: React.ReactNode;
}) {
  return (
    <View
      className="overflow-hidden rounded-[20px]"
      style={[{ backgroundColor: card, borderColor: border }, SOFT_SHADOW]}
    >
      {children}
    </View>
  );
}

/** Une ligne : icône sur pastille, libellé, badge éventuel, chevron — 56 points de haut. */
function Row({
  icon: Icon,
  label,
  badge,
  onPress,
  surface,
  accent,
  badgeColor,
  badgeText,
  danger,
  last,
}: {
  icon: LucideIcon;
  label: string;
  badge?: number;
  onPress: () => void;
  surface: ReturnType<typeof courierSurface>;
  accent: string;
  badgeColor?: string;
  badgeText?: string;
  danger?: boolean;
  last?: boolean;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      onPress={onPress}
      className="min-h-[56px] flex-row items-center gap-3 px-4"
      style={({ pressed }) => [
        last ? null : { borderBottomWidth: 1, borderBottomColor: surface.border },
        pressed ? { opacity: 0.6 } : null,
      ]}
    >
      <View
        className="h-10 w-10 items-center justify-center rounded-xl"
        style={{ backgroundColor: `${accent}1F` }}
      >
        <Icon size={20} color={accent} />
      </View>
      <Text
        className="flex-1 text-[16px]"
        style={{ color: danger ? accent : surface.text }}
        numberOfLines={1}
      >
        {label}
      </Text>
      {badge ? (
        <View
          className="min-w-[22px] items-center justify-center rounded-full px-1.5 py-0.5"
          style={{ backgroundColor: badgeColor ?? accent }}
        >
          <Text className="text-[12px] font-bold" style={{ color: badgeText ?? '#FFFFFF' }}>
            {badge > 99 ? '99+' : badge}
          </Text>
        </View>
      ) : null}
      {danger ? null : <ChevronRight size={18} color={surface.muted} className="flip-rtl" />}
    </Pressable>
  );
}
