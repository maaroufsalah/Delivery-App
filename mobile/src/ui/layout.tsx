import { type Href, useRouter } from 'expo-router';
import { ArrowLeft } from 'lucide-react-native';
import type { ReactNode } from 'react';
import { Image, Pressable, Text, View } from 'react-native';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';
import Svg, { Defs, LinearGradient, Rect, Stop } from 'react-native-svg';
import { BRAND_LOGO, BRAND_LOGO_RATIO } from '@/config/brand';
import { useTenantTheme } from '@/theme/tenant-theme';

/**
 * Les GABARITS d'écran — M4 bloc 1.
 *
 * La zone sûre est traitée ICI, une fois. Chaque écran qui la gérait lui-même
 * finissait par oublier un bord : sur un téléphone à encoche, le titre passait
 * sous l'heure, et sur Android à barre gestuelle, le dernier bouton d'une liste
 * se retrouvait sous la barre du système.
 */

/**
 * L'écran ordinaire : fond du thème, zone sûre en haut.
 *
 * **Pas de zone sûre en BAS** : la barre de navigation de l'application s'en
 * charge, et la doubler laisserait une bande vide de 34 points sur iPhone.
 * L'écran réserve la place par `bottomInset`, que les listes ajoutent à leur
 * `contentContainerStyle`.
 */
export function Screen({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <SafeAreaView edges={['top']} className={`flex-1 bg-background ${className}`}>
      {children}
    </SafeAreaView>
  );
}

/**
 * L'en-tête à DÉGRADÉ de la marque — M4, l'accueil.
 *
 * `expo-linear-gradient` n'est pas lié dans l'APK `ebb98c4b` et l'OTA ne peut
 * pas en ajouter : le dégradé est donc peint par `react-native-svg`, qui l'est.
 * Le rendu est identique — `Defs` + `LinearGradient` + deux `Stop` — et la
 * contrainte a un bénéfice : ce dégradé-là se teinte aussi bien sur un `Rect`
 * que dans une illustration, ce qu'un composant de vue ne permettait pas.
 *
 * Les deux bornes viennent de `brandGradient()` (`packages/shared`) : la
 * primaire du tenant, et la même poussée de 30 % vers sa secondaire. Rien n'est
 * choisi ici.
 */
export function GradientHeader({
  children,
  height = 168,
  logo = true,
}: {
  children: ReactNode;
  height?: number;
  /**
   * Le logo en haut de l'en-tête — dernières corrections, point 5. Blanc sur
   * le dégradé de la marque. L'écran de connexion le coupe : il l'affiche en
   * grand à la place du titre.
   */
  logo?: boolean;
}) {
  const { gradient } = useTenantTheme();
  const insets = useSafeAreaInsets();
  const total = height + insets.top;

  return (
    <View style={{ height: total }}>
      <View style={{ position: 'absolute', left: 0, right: 0, top: 0, height: total }}>
        <Svg width="100%" height="100%">
          <Defs>
            <LinearGradient id="brand" x1="0" y1="0" x2="1" y2="1">
              <Stop offset="0" stopColor={gradient.from} />
              <Stop offset="1" stopColor={gradient.to} />
            </LinearGradient>
          </Defs>
          <Rect x="0" y="0" width="100%" height="100%" fill="url(#brand)" />
        </Svg>
      </View>

      <View style={{ paddingTop: insets.top }} className="flex-1 justify-end px-5 pb-4">
        {logo ? (
          <Image
            source={BRAND_LOGO}
            accessibilityIgnoresInvertColors
            resizeMode="contain"
            style={{
              position: 'absolute',
              top: insets.top + 10,
              left: 20,
              height: 22,
              width: 22 * BRAND_LOGO_RATIO,
            }}
          />
        ) : null}
        {children}
      </View>
    </View>
  );
}

/** Le titre d'une section dans un écran qui défile. */
export function SectionTitle({ label, action }: { label: string; action?: ReactNode }) {
  return (
    <View className="flex-row items-center justify-between">
      <Text className="text-[17px] font-semibold text-foreground">{label}</Text>
      {action}
    </View>
  );
}

/**
 * L'EN-TÊTE D'UN ÉCRAN DE DÉTAIL — L2 bloc 1.
 *
 * Neuf des quinze maquettes ouvrent sur la même chose : une flèche de retour,
 * un titre, parfois une action à droite. C'était recopié écran par écran, avec
 * à chaque fois le risque d'oublier `flip-rtl` sur la flèche — et une flèche
 * qui pointe à gauche en arabe désigne l'avant, pas l'arrière.
 *
 * La cible de retour fait **44 points**, comme tout ce qui se touche ici : un
 * chevron de 20 points est visible, il n'est pas atteignable avec des gants.
 *
 * **Le repli, chantier « Bon de retour livreur » point 4.** `router.back()` ne
 * fait RIEN quand il n'y a rien derrière — et c'est le cas le plus fréquent de
 * tous : une notification ouvre l'application directement sur la fiche, et le
 * livreur se retrouve devant une flèche morte. `backHref` dit où aller dans ce
 * cas : l'onglet d'où cette fiche serait venue. Sans lui, le comportement ne
 * change pas d'un iota.
 */
export function ScreenHeader({
  title,
  subtitle,
  action,
  onBack,
  backHref,
}: {
  title: string;
  subtitle?: string;
  action?: ReactNode;
  onBack?: () => void;
  /** L'écran de repli quand il n'y a pas d'historique — une notification, un lien. */
  backHref?: Href;
}) {
  const router = useRouter();

  const goBack = () => {
    if (router.canGoBack()) {
      router.back();
      return;
    }

    // `replace` et non `push` : la fiche ne doit pas rester derrière l'onglet,
    // sinon le geste suivant y revient.
    if (backHref) router.replace(backHref);
  };

  return (
    <View className="flex-row items-center gap-2 px-4 pb-3 pt-3">
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={title}
        onPress={onBack ?? goBack}
        className="min-h-11 min-w-11 items-center justify-center rounded-full"
        hitSlop={8}
        style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
      >
        <ArrowLeft size={22} className="flip-rtl text-foreground" />
      </Pressable>

      <View className="flex-1">
        <Text className="text-[20px] font-bold text-foreground" numberOfLines={1}>
          {title}
        </Text>
        {subtitle ? (
          <Text className="text-[13px] text-muted-foreground" numberOfLines={1}>
            {subtitle}
          </Text>
        ) : null}
      </View>

      {action}
    </View>
  );
}
