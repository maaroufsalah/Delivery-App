import {
  DELIVERY_ADDRESS_MIN,
  type ExpectedPayment,
  isEditableBySeller,
  PARCEL_TEXT_MIN,
  recipientPhonesSchema,
} from '@delivery/shared';
import * as Haptics from 'expo-haptics';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useFreshKey } from '@/lib/use-fresh-key';
import { ArrowLeft } from 'lucide-react-native';
import { useEffect, useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { describeActionError } from '@/lib/use-action';
import { CitySelect } from '@/components/city-select';
import { ExpectedPaymentChoice } from '@/components/expected-payment-choice';
import { ApiError } from '@/lib/api';
import { useCities, useCompany, useParcel, useUpdateParcel } from '@/lib/merchant';
import { Button, Card, Field, Screen } from '@/ui';

/**
 * LE VENDEUR CORRIGE SON COLIS — recette SpeedyTrans 3, point 3.
 *
 * Les champs de la saisie, préremplis depuis la fiche. Possible tant que le
 * colis n'est pas ramassé : l'écran le dit et se ferme sinon, le serveur rend
 * 422 de toute façon. Changer de ville recalcule les frais côté serveur.
 */
function MerchantParcelEditScreenContent() {
  const { t } = useI18n();
  const router = useRouter();
  const { code } = useLocalSearchParams<{ code: string }>();

  const parcel = useParcel(code);
  const company = useCompany();
  const cities = useCities();
  const update = useUpdateParcel(code ?? '');

  const [form, setForm] = useState({
    description: '',
    recipientName: '',
    recipientPhone: '',
    destinationCityId: '',
    destinationDistrictName: '',
    deliveryAddress: '',
    cod: '',
    expectedPayment: 'cash' as ExpectedPayment,
  });
  const [note, setNote] = useState<string | null>(null);

  // Prérempli UNE fois, quand la fiche arrive : ensuite, c'est la saisie.
  const loaded = parcel.data;
  useEffect(() => {
    if (!loaded) return;
    setForm({
      description: loaded.description ?? '',
      recipientName: loaded.recipientName,
      recipientPhone: loaded.recipientPhone,
      destinationCityId: loaded.destinationCityId,
      destinationDistrictName: loaded.destinationDistrictName ?? '',
      deliveryAddress: loaded.deliveryAddress,
      cod: String(loaded.codAmount / 100),
      expectedPayment: loaded.expectedPayment ?? 'cash',
    });
  }, [loaded]);

  const set = <K extends keyof typeof form>(key: K, value: (typeof form)[K]) =>
    setForm((current) => ({ ...current, [key]: value }));

  const editable = loaded ? isEditableBySeller(loaded.statusId) : false;
  // D-244 : un à trois numéros dans le même champ, la règle du serveur.
  const phoneCheck = recipientPhonesSchema.safeParse(form.recipientPhone.trim());
  const tooManyPhones =
    !phoneCheck.success &&
    phoneCheck.error.issues.some((issue) => issue.message === 'too-many-phones');
  const ready =
    form.recipientName.trim().length >= PARCEL_TEXT_MIN &&
    phoneCheck.success &&
    form.description.trim().length >= PARCEL_TEXT_MIN &&
    Boolean(form.destinationCityId) &&
    form.deliveryAddress.trim().length >= DELIVERY_ADDRESS_MIN;

  const save = () => {
    update.mutate(
      {
        description: form.description.trim(),
        recipientName: form.recipientName.trim(),
        recipientPhone: form.recipientPhone.trim(),
        destinationCityId: form.destinationCityId,
        destinationDistrictName: form.destinationDistrictName.trim(),
        deliveryAddress: form.deliveryAddress.trim(),
        codAmount: Number(form.cod.replace(',', '.')) || 0,
        ...(company.data?.isPro ? { expectedPayment: form.expectedPayment } : {}),
      },
      {
        onSuccess: () => {
          void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
          router.back();
        },
        onError: (error) => {
          void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error);
          const reason = error instanceof ApiError ? error.reason : undefined;
          setNote(
            reason === 'parcel-picked-up'
              ? t('sellerEdit.pickedUp')
              : describeActionError(error, t),
          );
        },
      },
    );
  };

  return (
    <Screen>
      <View className="flex-row items-center gap-3 px-5 py-3">
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('common.close')}
          className="min-h-11 min-w-11 items-center justify-center"
          hitSlop={8}
          onPress={() => router.back()}
        >
          <ArrowLeft size={24} className="text-foreground" />
        </Pressable>
        <Text className="flex-1 text-[17px] font-semibold text-foreground">
          {t('sellerEdit.title', { code: code ?? '' })}
        </Text>
      </View>

      <KeyboardAvoidingView
        className="flex-1"
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-3 px-5 pb-8">
          {loaded && !editable ? (
            <Card>
              <Text className="text-[15px] text-foreground">{t('sellerEdit.pickedUp')}</Text>
            </Card>
          ) : (
            <>
              <Text className="text-[14px] text-muted-foreground">{t('sellerEdit.hint')}</Text>
              <Field
                label={t('new.recipient')}
                value={form.recipientName}
                onChangeText={(value) => set('recipientName', value)}
              />
              <Field
                label={t('new.phone')}
                value={form.recipientPhone}
                onChangeText={(value) => set('recipientPhone', value)}
                hint={tooManyPhones ? t('new.too-many-phones') : t('new.phonesHint')}
                inputMode="tel"
              />
              <CitySelect
                label={t('new.toCity')}
                cities={cities.data ?? []}
                value={form.destinationCityId}
                onChange={(id) => set('destinationCityId', id)}
                placeholder={t('new.choose')}
              />
              <Field
                label={t('new.district')}
                placeholder={t('new.districtPlaceholder')}
                value={form.destinationDistrictName}
                onChangeText={(value) => set('destinationDistrictName', value)}
              />
              <Field
                label={t('new.deliveryAddress')}
                value={form.deliveryAddress}
                onChangeText={(value) => set('deliveryAddress', value)}
              />
              <Field
                label={t('new.description')}
                value={form.description}
                onChangeText={(value) => set('description', value)}
              />
              <Field
                label={t('new.cod')}
                value={form.cod}
                onChangeText={(value) => set('cod', value)}
                inputMode="decimal"
              />
              {company.data?.isPro ? (
                <ExpectedPaymentChoice
                  value={form.expectedPayment}
                  onChange={(value) => set('expectedPayment', value)}
                />
              ) : null}

              {note ? <Text className="text-[14px] text-destructive">{note}</Text> : null}

              <Button
                label={t('sellerEdit.save')}
                onPress={save}
                busy={update.isPending}
                disabled={!ready || !loaded}
                full
              />
            </>
          )}
        </ScrollView>
      </KeyboardAvoidingView>
    </Screen>
  );
}

/** Coque Recette 7 (point 5) : le contenu repart vide à chaque `code` et à chaque fermeture — `useFreshKey`. */
export default function MerchantParcelEditScreen() {
  const { code } = useLocalSearchParams<{ code: string }>();
  return <MerchantParcelEditScreenContent key={useFreshKey(code)} />;
}
