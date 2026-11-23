import type { LucideIcon } from 'lucide-react-native';
import type { ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';
import Svg, { Circle } from 'react-native-svg';
import { useTokens } from '@/theme/tenant-theme';

/**
 * Les SURFACES — M4 bloc 1.
 *
 * Une carte, une puce, une tuile de chiffre, un badge de statut. Tout ce qui
 * pose du contenu sur un fond, et rien qui décide quoi que ce soit.
 */

/**
 * La carte.
 *
 * **Une bordure plutôt qu'une ombre.** `elevation` sur Android et `shadow*` sur
 * iOS ne se ressemblent pas, et une ombre portée sur un fond sombre ne se voit
 * pas du tout — on obtiendrait donc trois rendus pour un composant. Une bordure
 * d'un point fonctionne partout et dans les deux modes.
 */
export function Card({
  children,
  className = '',
  onPress,
}: {
  children: ReactNode;
  className?: string;
  onPress?: () => void;
}) {
  const style = `rounded-2xl border border-border bg-card p-4 ${className}`;

  if (!onPress) return <View className={style}>{children}</View>;

  return (
    <Pressable
      accessibilityRole="button"
      onPress={onPress}
      className={style}
      style={({ pressed }) => (pressed ? { opacity: 0.85 } : null)}
    >
      {children}
    </Pressable>
  );
}

/**
 * Une puce de filtre — M4, l'écran Colis.
 *
 * 40 points de haut, ce qui est sous les 44 de la règle : une puce est en
 * BANDE, côte à côte avec ses sœurs, et la cible réelle inclut l'espace entre
 * elles. Le compromis est assumé ici et nulle part ailleurs — il permet de
 * montrer six filtres sans faire défiler.
 */
export function Chip({
  label,
  active,
  count,
  onPress,
}: {
  label: string;
  active?: boolean;
  count?: number;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ selected: active }}
      onPress={onPress}
      /*
       * HAUTEUR ET RAYON FIXES — C15 bloc 5.
       *
       * `min-h-10` + `rounded-full` donnait une puce dont la forme dépendait de
       * son contenu : une étiquette qui passe à la ligne la faisait grandir, et
       * le rayon suivait. Sur un écran où quatre puces se suivent, trois
       * étaient des pilules et la quatrième un rectangle arrondi.
       *
       * 36 de haut, 18 de rayon : exactement la moitié, donc une pilule
       * parfaite, identique sur tous les écrans et dans les deux modes.
       */
      className={`h-9 flex-row items-center gap-1.5 rounded-[18px] border px-3.5 ${
        active ? 'border-primary bg-primary' : 'border-border bg-card'
      }`}
    >
      <Text
        className={`text-[15px] ${active ? 'font-semibold text-primary-foreground' : 'text-foreground'}`}
      >
        {label}
      </Text>
      {typeof count === 'number' ? (
        <Text
          className={`text-[13px] ${active ? 'text-primary-foreground' : 'text-muted-foreground'}`}
        >
          {count}
        </Text>
      ) : null}
    </Pressable>
  );
}

/**
 * La tuile de chiffre du jour — M4, l'accueil livreur.
 *
 * Le CHIFFRE d'abord, le libellé dessous : c'est ce qu'on lit à un mètre, d'un
 * coup d'œil, avant de partir. L'ordre inverse oblige à lire pour trouver.
 */
export function KpiTile({
  label,
  value,
  icon: Icon,
  tone = 'default',
  color,
  width = 'half',
  onPress,
}: {
  label: string;
  value: string;
  icon?: LucideIcon;
  tone?: 'default' | 'primary' | 'success' | 'warning';
  /**
   * La couleur EXACTE de l'accent, quand elle est donnée par le référentiel.
   *
   * L'accueil compte des FAMILLES de statuts, et leurs couleurs vivent dans
   * `packages/shared/src/statuses.ts` (CLAUDE.md règle 3). Les faire passer par
   * une des quatre tonalités du thème aurait peint « en retour » et « à
   * ramasser » de la même teinte, alors que le livreur les distingue d'un coup
   * d'œil sur toutes les autres listes de l'application.
   */
  color?: string;
  /** Deux tuiles par ligne, ou trois quand la rangée en porte trois. */
  width?: 'half' | 'third';
  onPress?: () => void;
}) {
  const accents: Record<string, string> = {
    default: 'text-muted-foreground',
    primary: 'text-primary',
    success: 'text-success',
    warning: 'text-warning',
  };

  return (
    <Card
      className={`${width === 'third' ? 'min-w-[27%]' : 'min-w-[44%]'} flex-1 gap-1 p-3.5`}
      onPress={onPress}
    >
      <View className="flex-row items-center justify-between">
        <Text className="text-[26px] font-bold text-foreground">{value}</Text>
        {Icon ? (
          color ? (
            <Icon size={18} color={color} />
          ) : (
            <Icon size={18} className={accents[tone]} />
          )
        ) : null}
      </View>
      <Text className="text-[13px] text-muted-foreground" numberOfLines={2}>
        {label}
      </Text>
    </Card>
  );
}

/** Deux tuiles par ligne, qui se réarrangent sur un petit écran. */
export function KpiRow({ children }: { children: ReactNode }) {
  return <View className="flex-row flex-wrap gap-3">{children}</View>;
}

/** Un badge de statut. La couleur vient du référentiel, jamais d'ici. */
export function StatusBadge({ label, color }: { label: string; color: string }) {
  return (
    <View
      className="self-start rounded-full px-2.5 py-1"
      // La seule couleur inline du système, et elle est DONNÉE : c'est celle du
      // statut, définie dans `packages/shared/statuses.ts`. Le suffixe `22` est
      // l'alpha — un fond teinté à 13 %, lisible dans les deux modes.
      style={{ backgroundColor: `${color}22` }}
    >
      <Text className="text-[13px] font-medium" style={{ color }}>
        {label}
      </Text>
    </View>
  );
}

/** Un filet, quand deux blocs ont besoin d'être séparés sans être encadrés. */
export function Divider() {
  return <View className="h-px bg-border" />;
}

/**
 * La PROGRESSION d'une tournée — M4 bloc 4.
 *
 * Deux vues superposées, pas un SVG : une barre est un rectangle dans un
 * rectangle, et la largeur se dit en pourcentage. Un tracé vectoriel devrait
 * mesurer son conteneur pour faire la même chose.
 *
 * La barre est **arrondie aux deux bouts** et ne descend jamais sous quelques
 * points de large quand la valeur est faible : une progression de 3 % qui rend
 * un trait invisible se lit comme « rien n'a commencé », ce qui est faux.
 */
export function ProgressBar({ value, total }: { value: number; total: number }) {
  const ratio = total > 0 ? Math.min(1, Math.max(0, value / total)) : 0;

  return (
    <View className="h-2 w-full overflow-hidden rounded-full bg-muted">
      <View
        className="h-full rounded-full bg-primary"
        style={{ width: `${Math.max(ratio * 100, ratio > 0 ? 4 : 0)}%` }}
      />
    </View>
  );
}

/**
 * L'ANNEAU DE PERFORMANCE — L2 bloc 1, maquette 2.
 *
 * Le taux de livraison est le seul chiffre de l'accueil qu'on ne lit pas en
 * valeur absolue : « 23 » ne veut rien dire, « 23 sur 25 » veut dire beaucoup.
 * L'anneau montre le rapport AVANT le nombre — on voit s'il reste un creux à
 * combler sans avoir à faire la division.
 *
 * **Un SVG ici, une barre ailleurs, et ce n'est pas une inconséquence.** Une
 * barre de progression est un rectangle dans un rectangle, dont la largeur se
 * dit en pourcentage ; un arc n'a pas d'équivalent en mise en page — il faut le
 * tracer. `strokeDasharray` fait tout le travail : la longueur du trait plein
 * est la fraction de la circonférence, le reste est le vide.
 *
 * **Le tracé part en HAUT** (`rotation: -90`) et non à droite, où un cercle SVG
 * commence : un anneau qui se remplit depuis trois heures se lit de travers.
 */
export function ProgressRing({
  percent,
  size = 84,
  thickness = 9,
}: {
  percent: number;
  size?: number;
  thickness?: number;
}) {
  const tokens = useTokens();
  const bounded = Math.min(100, Math.max(0, percent));
  const radius = (size - thickness) / 2;
  const circumference = 2 * Math.PI * radius;

  return (
    <View style={{ width: size, height: size }} className="items-center justify-center">
      <Svg width={size} height={size} style={{ transform: [{ rotate: '-90deg' }] }}>
        <Circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          stroke={tokens.muted}
          strokeWidth={thickness}
          fill="none"
        />
        <Circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          stroke={tokens.primary}
          strokeWidth={thickness}
          strokeLinecap="round"
          fill="none"
          strokeDasharray={`${(circumference * bounded) / 100} ${circumference}`}
        />
      </Svg>

      {/*
        Le pourcentage est POSÉ par-dessus, pas inscrit dans le SVG : un
        `<Text>` de React Native suit la taille de police du système, un
        `<SvgText>` ne la suit pas — et un livreur qui a grossi ses polices l'a
        fait pour lire, y compris ici.
      */}
      <View className="absolute items-center justify-center">
        <Text className="text-[20px] font-bold text-foreground">{Math.round(bounded)} %</Text>
      </View>
    </View>
  );
}
