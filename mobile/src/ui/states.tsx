import type { LucideIcon } from 'lucide-react-native';
import { useEffect } from 'react';
import { Text, View } from 'react-native';
import Animated, {
  useAnimatedStyle,
  useSharedValue,
  withRepeat,
  withTiming,
} from 'react-native-reanimated';
import Svg, { Circle, Path, Rect } from 'react-native-svg';
import { useTokens } from '@/theme/tenant-theme';

/**
 * Les états D'ATTENTE et de VIDE — M4 bloc 1.
 *
 * Les deux moments où un écran n'a rien à montrer, et les deux qu'on bâcle le
 * plus souvent. Un écran qui ne dit rien pendant deux secondes est indiscernable
 * d'un écran cassé ; une liste vide sans un mot l'est d'une panne de réseau.
 */

/**
 * Le squelette d'un bloc, pendant le chargement.
 *
 * **Il respire.** Une forme grise immobile ressemble à un défaut d'affichage ;
 * la même qui pulse lentement dit « ça arrive ». L'animation vit sur le fil
 * d'interface via Reanimated, donc elle ne tombe pas quand le JS travaille —
 * c'est-à-dire précisément pendant un chargement.
 */
export function Skeleton({ className = '' }: { className?: string }) {
  const opacity = useSharedValue(0.55);

  useEffect(() => {
    opacity.value = withRepeat(withTiming(1, { duration: 850 }), -1, true);
  }, [opacity]);

  const style = useAnimatedStyle(() => ({ opacity: opacity.value }));

  return <Animated.View className={`rounded-2xl bg-muted ${className}`} style={style} />;
}

/** Le squelette d'une carte de liste : deux lignes et un badge. */
export function SkeletonCard() {
  return (
    <View className="gap-2.5 rounded-2xl border border-border bg-card p-4">
      <Skeleton className="h-4 w-1/3" />
      <Skeleton className="h-3.5 w-2/3" />
      <Skeleton className="h-6 w-24 rounded-full" />
    </View>
  );
}

/**
 * Un écran vide, ILLUSTRÉ — M4.
 *
 * Le dessin est un SVG écrit ici, pas un fichier : l'OTA n'embarque pas de
 * nouvel asset binaire, et une illustration qui lit les jetons du thème se
 * teinte toute seule à la marque du client. Trois variantes suffisent à couvrir
 * les listes de l'application.
 */
export function EmptyState({
  icon: Icon,
  art = 'box',
  title,
  hint,
}: {
  icon?: LucideIcon;
  art?: 'box' | 'search' | 'route' | 'none';
  title: string;
  hint?: string;
}) {
  return (
    <View className="flex-1 items-center justify-center gap-3 px-8 py-12">
      {art === 'none' ? (
        Icon ? (
          <Icon size={48} className="text-muted-foreground" />
        ) : null
      ) : (
        <EmptyArt kind={art} />
      )}
      <Text className="text-center text-[18px] font-semibold text-foreground">{title}</Text>
      {hint ? <Text className="text-center text-[16px] text-muted-foreground">{hint}</Text> : null}
    </View>
  );
}

/**
 * Le dessin, en trois motifs.
 *
 * Volontairement pauvre en traits : une illustration détaillée vieillit mal et
 * détonne avec le reste d'une interface d'outil. Ce qu'on cherche est de
 * remplir le vide avec quelque chose qui appartient à la marque — d'où les
 * jetons plutôt que des gris.
 */
function EmptyArt({ kind }: { kind: 'box' | 'search' | 'route' }) {
  const tokens = useTokens();
  const line = tokens['muted-foreground'];
  const fill = tokens.muted;
  const brand = tokens.primary;

  return (
    <Svg width={132} height={104} viewBox="0 0 132 104" accessibilityRole="image">
      {kind === 'box' ? (
        <>
          <Rect
            x={30}
            y={40}
            width={72}
            height={48}
            rx={6}
            fill={fill}
            stroke={line}
            strokeWidth={2}
          />
          <Path d="M30 52 H102" stroke={line} strokeWidth={2} />
          <Rect x={57} y={40} width={18} height={12} rx={2} fill={brand} />
          <Path d="M46 40 L66 22 L86 40" stroke={line} strokeWidth={2} fill="none" />
        </>
      ) : null}

      {kind === 'search' ? (
        <>
          <Circle cx={58} cy={48} r={24} fill={fill} stroke={line} strokeWidth={2} />
          <Path d="M76 66 L96 86" stroke={brand} strokeWidth={4} strokeLinecap="round" />
          <Path d="M48 48 H68" stroke={line} strokeWidth={2} strokeLinecap="round" />
          <Path d="M48 56 H62" stroke={line} strokeWidth={2} strokeLinecap="round" />
        </>
      ) : null}

      {kind === 'route' ? (
        <>
          <Path
            d="M34 84 C34 56 98 62 98 34"
            stroke={line}
            strokeWidth={2}
            strokeDasharray="6 6"
            fill="none"
          />
          <Circle cx={34} cy={84} r={7} fill={fill} stroke={line} strokeWidth={2} />
          <Circle cx={98} cy={30} r={9} fill={brand} />
        </>
      ) : null}
    </Svg>
  );
}
