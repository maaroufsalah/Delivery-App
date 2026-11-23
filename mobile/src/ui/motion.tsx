import type { ReactNode } from 'react';
import { AccessibilityInfo } from 'react-native';
import { useEffect, useState } from 'react';
import Animated, { FadeInDown } from 'react-native-reanimated';

/**
 * L'APPARITION d'une carte — M4 bloc 1.
 *
 * Un décalage de 40 ms par rang : les cartes d'une liste arrivent l'une après
 * l'autre, ce qui donne l'impression que l'écran se remplit plutôt qu'il ne
 * clignote. Au-delà de six rangs le décalage est plafonné — sinon la dernière
 * carte d'une liste de trente arriverait une seconde après la première.
 *
 * **`reduce-motion` est respecté**, comme le web le fait (M1 le demandait déjà,
 * et c'est une exigence d'accessibilité des deux magasins) : quand le système
 * annonce que l'animation dérange, le composant ne rend qu'une vue.
 */
export function FadeIn({ children, index = 0 }: { children: ReactNode; index?: number }) {
  const [reduced, setReduced] = useState(false);

  useEffect(() => {
    let alive = true;

    void AccessibilityInfo.isReduceMotionEnabled().then((value) => {
      if (alive) setReduced(value);
    });

    const subscription = AccessibilityInfo.addEventListener('reduceMotionChanged', setReduced);

    return () => {
      alive = false;
      subscription.remove();
    };
  }, []);

  if (reduced) return <>{children}</>;

  return (
    <Animated.View entering={FadeInDown.delay(Math.min(index, 6) * 40).duration(260)}>
      {children}
    </Animated.View>
  );
}
