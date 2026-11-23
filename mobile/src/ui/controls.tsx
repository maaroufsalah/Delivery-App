import { cssInterop } from 'nativewind';
import type { LucideIcon } from 'lucide-react-native';
import * as Haptics from 'expo-haptics';
import { useRef } from 'react';
import {
  ActivityIndicator,
  Pressable,
  Text,
  TextInput,
  View,
  type TextInputProps,
} from 'react-native';
import { useTokens } from '@/theme/tenant-theme';

/** Les icônes de `lucide-react-native` acceptent `className` après cet appel. */
export function iconClass(Icon: LucideIcon): LucideIcon {
  cssInterop(Icon, { className: { target: 'style', nativeStyleToProp: { color: true } } });
  return Icon;
}

type ButtonTone = 'primary' | 'ghost' | 'danger' | 'soft';

/** Deux appuis plus rapprochés que ça sont un double appui. */
const DOUBLE_PRESS_MS = 700;

/**
 * Le bouton — M1, enrichi par M4 d'un ton « soft » et du retour haptique.
 *
 * **L'haptique sur l'action principale seulement.** Un téléphone qui vibre à
 * chaque appui devient une nuisance qu'on désactive dans les réglages système,
 * et on perd alors aussi celui du scan, qui lui compte.
 */
export function Button({
  label,
  onPress,
  tone = 'primary',
  icon: Icon,
  disabled,
  busy,
  full,
}: {
  label: string;
  onPress: () => void;
  tone?: ButtonTone;
  icon?: LucideIcon;
  disabled?: boolean;
  busy?: boolean;
  full?: boolean;
}) {
  const tones: Record<ButtonTone, string> = {
    primary: 'bg-primary',
    soft: 'bg-accent',
    ghost: 'bg-transparent border border-border',
    danger: 'bg-destructive',
  };

  const labels: Record<ButtonTone, string> = {
    primary: 'text-primary-foreground',
    soft: 'text-accent-foreground',
    ghost: 'text-foreground',
    danger: 'text-destructive-foreground',
  };

  /*
   * APPUI UNIQUE, pour tous les boutons — Recette 5, point 4 a. `busy` arrive
   * au rendu SUIVANT l'appui (`isPending`) ; un double appui tombait entre les
   * deux et partait deux fois. Le verrou est synchrone et dure 700 ms : assez
   * pour un double appui, trop court pour gêner un geste voulu.
   */
  const lastPress = useRef(0);

  const press = () => {
    const now = Date.now();
    if (now - lastPress.current < DOUBLE_PRESS_MS) return;
    lastPress.current = now;

    if (tone === 'primary' || tone === 'danger') {
      void Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light);
    }
    onPress();
  };

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled: disabled || busy }}
      disabled={disabled || busy}
      onPress={press}
      // `min-h-[52px]` et non 44 : les actions principales sont en bas d'écran,
      // là où le pouce arrive le moins précisément.
      className={`min-h-[52px] flex-row items-center justify-center gap-2 rounded-2xl px-5 ${
        tones[tone]
      } ${full ? 'w-full' : ''} ${disabled || busy ? 'opacity-50' : ''}`}
      style={({ pressed }) => (pressed ? { transform: [{ scale: 0.98 }] } : null)}
    >
      {busy ? <ActivityIndicator /> : Icon ? <Icon size={20} className={labels[tone]} /> : null}
      <Text className={`text-[16px] font-semibold ${labels[tone]}`}>{label}</Text>
    </Pressable>
  );
}

/**
 * Un champ de saisie — M1.
 *
 * `placeholderTextColor` n'accepte pas de classe : c'est exactement le cas
 * pour lequel `useTokens()` existe. M1 y passait la chaîne `'var(--…)'`, que
 * React Native ne résout pas — le texte indicatif prenait donc la couleur par
 * défaut de la plateforme, invisible en sombre.
 */
export function Field({
  label,
  hint,
  ...props
}: TextInputProps & { label?: string; hint?: string }) {
  const tokens = useTokens();

  return (
    <View className="gap-1.5">
      {label ? (
        <Text className="text-[14px] font-medium text-muted-foreground">{label}</Text>
      ) : null}
      <TextInput
        accessibilityLabel={label}
        // 52 points : une saisie se fait souvent debout, et un champ de 40
        // points se rate une fois sur trois.
        className="min-h-[52px] rounded-2xl border border-border bg-card px-4 text-[16px] text-foreground"
        placeholderTextColor={tokens['muted-foreground']}
        {...props}
      />
      {hint ? <Text className="text-[13px] text-muted-foreground">{hint}</Text> : null}
    </View>
  );
}
