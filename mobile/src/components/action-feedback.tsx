import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import type { ActionFeedback as Feedback } from '@/lib/failure';

export type { ActionFeedback as Feedback } from '@/lib/failure';

/**
 * LA LIGNE DE RETOUR D'UN GESTE — Recette 7, point 3.
 *
 * Un seul rendu pour « ajouté », « déjà coché », « pas sur ce bon », « pas de
 * réseau » : la couleur dit la gravité (vert, orange, rouge), la phrase dit
 * le métier, et la référence technique n'apparaît que si on la demande
 * (« Détails ») — sauf sur une panne, où elle est là d'emblée pour le support.
 *
 * `action` est la sortie proposée : « Ouvrir BR-2610-0012 » quand le colis
 * scanné est sur un autre bon du même circuit.
 */
export function ActionFeedbackLine({
  feedback,
  fallback,
  center,
}: {
  feedback: Feedback | null | undefined;
  /** Ce qu'on lit quand il n'y a rien à dire (« Visez le code-barres… »). */
  fallback?: string;
  center?: boolean;
}) {
  const { t } = useI18n();
  const [details, setDetails] = useState(false);

  if (!feedback) {
    return fallback ? (
      <Text className={`text-[16px] text-muted-foreground ${center ? 'text-center' : ''}`}>
        {fallback}
      </Text>
    ) : null;
  }

  const colour =
    feedback.tone === 'ok'
      ? 'text-success'
      : feedback.tone === 'warn'
        ? 'text-warning'
        : 'text-destructive';
  const technical = feedback.severity === 'technical';
  const showReference = Boolean(feedback.reference) && (technical || details);

  return (
    <View className={`gap-1 ${center ? 'items-center' : ''}`}>
      <Text className={`text-[16px] font-medium ${colour} ${center ? 'text-center' : ''}`}>
        {feedback.text}
      </Text>

      {showReference ? (
        <Text
          className={`text-[12px] text-muted-foreground ${center ? 'text-center' : ''}`}
          selectable
        >
          {t('action.reference', { reference: feedback.reference ?? '' })}
        </Text>
      ) : null}

      {feedback.action || (feedback.reference && !technical) ? (
        <View className={`flex-row flex-wrap gap-3 ${center ? 'justify-center' : ''}`}>
          {feedback.action ? (
            <Pressable
              accessibilityRole="button"
              onPress={feedback.action.onPress}
              hitSlop={8}
              className="bg-accent min-h-10 justify-center rounded-xl px-3"
              style={({ pressed }) => (pressed ? { opacity: 0.7 } : null)}
            >
              <Text className="text-accent-foreground text-[14px] font-semibold">
                {feedback.action.label}
              </Text>
            </Pressable>
          ) : null}
          {feedback.reference && !technical ? (
            <Pressable
              accessibilityRole="button"
              onPress={() => setDetails((open) => !open)}
              hitSlop={8}
              className="min-h-10 justify-center"
            >
              <Text className="text-[13px] text-muted-foreground underline">
                {details ? t('action.hideDetails') : t('action.details')}
              </Text>
            </Pressable>
          ) : null}
        </View>
      ) : null}
    </View>
  );
}
