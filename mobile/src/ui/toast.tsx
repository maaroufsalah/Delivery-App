import { CheckCircle2, Info, OctagonX, TriangleAlert } from 'lucide-react-native';
import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { Text } from 'react-native';
import Animated, { FadeInUp, FadeOutUp } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTokens } from '@/theme/tenant-theme';

/**
 * Les TOASTS — M4 bloc 1.
 *
 * **Pourquoi pas `Alert.alert`.** Une alerte système bloque : elle attend un
 * appui pour disparaître, au milieu d'un geste qui a déjà réussi. « Statut
 * enregistré » n'a pas à coûter un appui. L'application en posait pourtant
 * partout, faute d'autre chose.
 *
 * **En HAUT, pas en bas.** Le bas est occupé par la barre de navigation et par
 * les boutons d'action ; un toast y recouvrirait ce sur quoi on vient
 * d'appuyer. En haut, il tombe sous l'en-tête et repart tout seul.
 */

type ToastTone = 'success' | 'error' | 'warning' | 'info';

interface ToastState {
  id: number;
  message: string;
  tone: ToastTone;
}

interface ToastApi {
  show: (message: string, tone?: ToastTone) => void;
}

const ToastContext = createContext<ToastApi>({ show: () => undefined });

export function useToast(): ToastApi {
  return useContext(ToastContext);
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toast, setToast] = useState<ToastState | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const show = useCallback((message: string, tone: ToastTone = 'success') => {
    if (timer.current) clearTimeout(timer.current);

    setToast({ id: Date.now(), message, tone });
    // Trois secondes : le temps de lire une phrase courte sans que le message
    // reste en travers de l'écran suivant.
    timer.current = setTimeout(() => setToast(null), 3000);
  }, []);

  const api = useMemo(() => ({ show }), [show]);

  return (
    <ToastContext.Provider value={api}>
      {children}
      {toast ? <Toast key={toast.id} message={toast.message} tone={toast.tone} /> : null}
    </ToastContext.Provider>
  );
}

export function Toast({ message, tone = 'success' }: { message: string; tone?: ToastTone }) {
  const insets = useSafeAreaInsets();
  const tokens = useTokens();

  /*
   * Recette 6, point 4 : une couleur par type — vert succès, rouge erreur,
   * orange alerte, couleur de la marque pour une information (elle était
   * noire) — et un format plus grand. Les premiers plans sont ceux du thème,
   * lisibles garantis (`readableForeground`, D-046).
   */
  const palette: Record<ToastTone, { background: string; foreground: string }> = {
    success: { background: tokens.success, foreground: tokens['success-foreground'] },
    error: { background: tokens.destructive, foreground: tokens['destructive-foreground'] },
    warning: { background: tokens.warning, foreground: tokens['warning-foreground'] },
    info: { background: tokens.primary, foreground: tokens['primary-foreground'] },
  };
  const icons = { success: CheckCircle2, error: OctagonX, warning: TriangleAlert, info: Info };

  const Icon = icons[tone];
  const colors = palette[tone];

  return (
    <Animated.View
      entering={FadeInUp.duration(200)}
      exiting={FadeOutUp.duration(160)}
      pointerEvents="none"
      accessibilityLiveRegion="polite"
      className="absolute inset-x-4 min-h-14 flex-row items-center gap-3 rounded-2xl px-4 py-3.5"
      style={{
        top: insets.top + 8,
        backgroundColor: colors.background,
        // Une ombre portée : le toast se détache du contenu, clair comme sombre.
        shadowColor: '#000',
        shadowOpacity: 0.18,
        shadowRadius: 12,
        shadowOffset: { width: 0, height: 4 },
        elevation: 6,
      }}
    >
      <Icon size={24} color={colors.foreground} />
      <Text
        className="flex-1 text-[16px] font-semibold leading-snug"
        style={{ color: colors.foreground }}
      >
        {message}
      </Text>
    </Animated.View>
  );
}
