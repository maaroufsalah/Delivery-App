import { EXPECTED_PAYMENTS, type ExpectedPayment } from '@delivery/shared';
import { Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { Chip } from '@/ui';

/**
 * « PAIEMENT ATTENDU : ESPÈCES / CHÈQUE » — recette SpeedyTrans 3, point 3.
 *
 * Pour un client Pro seulement, Espèces par défaut. Deux puces : il n'y a que
 * deux réponses, et on les voit toutes les deux. Même règle que le web ; le
 * serveur la tient de toute façon.
 */
export function ExpectedPaymentChoice({
  value,
  onChange,
}: {
  value: ExpectedPayment;
  onChange: (value: ExpectedPayment) => void;
}) {
  const { t } = useI18n();

  return (
    <View className="gap-2">
      <Text className="text-[14px] font-medium text-muted-foreground">
        {t('parcel.expectedPayment')}
      </Text>
      <View className="flex-row gap-2">
        {EXPECTED_PAYMENTS.map((option) => (
          <Chip
            key={option}
            label={t(`expectedPayment.${option}`)}
            active={value === option}
            onPress={() => onChange(option)}
          />
        ))}
      </View>
    </View>
  );
}
