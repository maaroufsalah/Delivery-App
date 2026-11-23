import {
  DELIVERY_ADDRESS_MIN,
  districtKey,
  PARCEL_TEXT_MIN,
  PICKUP_ADDRESS_MIN,
  recipientPhonesSchema,
} from '@delivery/shared';
import * as Haptics from 'expo-haptics';
import { useRouter } from 'expo-router';
import { useFreshKey } from '@/lib/use-fresh-key';
import { ArrowLeft, Check } from 'lucide-react-native';
import { useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  Switch,
  Text,
  View,
} from 'react-native';
import { useI18n } from '@/i18n';
import { describeActionError } from '@/lib/use-action';
import { CitySelect } from '@/components/city-select';
import { ExpectedPaymentChoice } from '@/components/expected-payment-choice';
import {
  type Choice,
  isDeferred,
  type NewParcel,
  useCities,
  useCompany,
  useCreateParcel,
  useDistricts,
  useQuote,
} from '@/lib/merchant';
import { Button, Card, Chip, Field, Screen } from '@/ui';

/**
 * CRÉER UN COLIS, en trois étapes — M2 bloc 5.
 *
 * **Trois écrans plutôt qu'un long formulaire.** Quinze champs d'affilée sur un
 * téléphone, c'est un pouce qui fait défiler et un clavier qui recouvre la
 * moitié de ce qu'on saisit. Le découpage suit l'ordre dans lequel un vendeur
 * a l'information sous les yeux : qui, où, quoi et combien.
 *
 * **Chaque étape se valide avant la suivante.** Une erreur découverte à la fin
 * obligerait à remonter trois écrans pour la corriger — et c'est le téléphone
 * du destinataire qui se trompe le plus souvent, parce qu'il est recopié d'une
 * commande.
 *
 * **Le tarif s'affiche dès que les deux villes sont connues**, sur la troisième
 * étape, à côté du montant à encaisser. C'est là qu'il sert : le vendeur décide
 * de ce qu'il demande au client en sachant ce que la course lui coûte.
 */
function NewParcelScreenContent() {
  const { t } = useI18n();
  const router = useRouter();

  const [step, setStep] = useState(1);
  const [note, setNote] = useState<string | null>(null);

  const [form, setForm] = useState<NewParcel>({
    description: '',
    recipientName: '',
    recipientPhone: '',
    originCityId: '',
    pickupAddress: '',
    destinationCityId: '',
    destinationDistrictName: '',
    deliveryAddress: '',
    codAmount: 0,
    openOnDelivery: false,
    fragile: false,
  });

  const [cod, setCod] = useState('');

  const cities = useCities();
  const districts = useDistricts(form.destinationCityId || undefined);
  const create = useCreateParcel();

  /*
   * LA FICHE DU VENDEUR PRÉREMPLIT LE DÉPART — retouches Anass, point 9.
   *
   * « Ne plus afficher le choix "Ville de départ" par défaut. La ville et
   * l'adresse de ramassage sont celles de la fiche du vendeur. » L'écran
   * posait les deux questions à chaque colis, et la réponse était toujours la
   * même — sauf le jour où elle ne l'est pas, d'où l'option plutôt que le
   * retrait pur et simple.
   *
   * `GET /merchant/me/company` est déjà chargée par l'écran « Ma société » et
   * mise en cache par TanStack Query : ce n'est pas un appel de plus au
   * premier colis de la journée, c'est le même.
   */
  const company = useCompany();

  const recordAddress = (company.data?.address ?? '').trim();
  /** Le seuil est celui de `createParcelSchema` : non vide (D-131, recette 4). */
  const recordUsable = Boolean(company.data) && recordAddress.length >= PICKUP_ADDRESS_MIN;

  /*
   * `null` = « le vendeur n'a rien dit ». L'option est alors DÉDUITE : cochée
   * si la fiche n'a pas d'adresse exploitable, décochée sinon.
   *
   * Ce détour par trois états évite un effet qui pousserait la fiche dans le
   * formulaire à son arrivée — la fiche arrive après le premier rendu, et un
   * `setState` dans un effet déclenche une cascade de rendus (règle du
   * compilateur React, et D-011). Ici RIEN n'est recopié : la ville et
   * l'adresse effectives se DÉRIVENT juste en dessous.
   */
  const [elsewhereChoice, setElsewhereChoice] = useState<boolean | null>(null);
  const elsewhere = elsewhereChoice ?? !recordUsable;
  const fromRecord = Boolean(company.data) && !elsewhere;

  /**
   * LE DÉPART EFFECTIF — celui qui part au serveur et qui fixe le tarif.
   *
   * Décochée, c'est la fiche ; cochée, c'est ce que le vendeur a saisi. Une
   * seule expression, donc aucun risque de voir l'écran annoncer une ville et
   * le colis en emporter une autre.
   */
  const origin = fromRecord
    ? { originCityId: company.data?.cityId ?? '', pickupAddress: recordAddress }
    : { originCityId: form.originCityId, pickupAddress: form.pickupAddress };

  const quote = useQuote({
    originCityId: origin.originCityId || undefined,
    destinationCityId: form.destinationCityId || undefined,
    /*
     * `district` : c'est ce que le serveur applique à la création (défaut de
     * `createParcelSchema`, que cet écran ne change pas). Le quartier étant
     * désormais facultatif (chantier « Quartier en saisie libre »), l'afficher
     * au tarif agence quand il est vide annoncerait un prix qui n'est pas
     * celui du colis.
     */
    deliveryTarget: 'district',
  });

  const set = <K extends keyof NewParcel>(key: K, value: NewParcel[K]) =>
    setForm((current) => ({ ...current, [key]: value }));

  /**
   * Ce qui manque pour passer à l'étape suivante.
   *
   * Les mêmes bornes que `createParcelSchema`, côté partagé : trois caractères
   * pour un nom, cinq pour l'adresse du destinataire, une adresse de ramassage
   * non vide, et le format marocain pour le téléphone. Les répéter ici évite un aller-retour serveur pour dire ce
   * qu'on savait déjà — le serveur revérifie, lui, parce que c'est lui qui
   * décide.
   */
  /*
   * D-244 : un à trois numéros dans le même champ, la règle du serveur
   * (`recipientPhonesSchema`). Plus de trois : l'aide du champ le dit, au lieu
   * de laisser le bouton « Suivant » éteint sans raison lisible.
   */
  const phoneCheck = recipientPhonesSchema.safeParse(form.recipientPhone.trim());
  const tooManyPhones =
    !phoneCheck.success &&
    phoneCheck.error.issues.some((issue) => issue.message === 'too-many-phones');

  const stepReady =
    step === 1
      ? form.recipientName.trim().length >= PARCEL_TEXT_MIN &&
        phoneCheck.success &&
        form.description.trim().length >= PARCEL_TEXT_MIN
      : step === 2
        ? Boolean(origin.originCityId) &&
          origin.pickupAddress.trim().length >= PICKUP_ADDRESS_MIN &&
          Boolean(form.destinationCityId) &&
          form.deliveryAddress.trim().length >= DELIVERY_ADDRESS_MIN
        : true;

  const submit = () => {
    create.mutate(
      { ...form, ...origin, codAmount: Number(cod.replace(',', '.')) || 0 },
      {
        onSuccess: (result) => {
          void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);

          if (isDeferred(result)) {
            // Le geste est acquis pour le vendeur : l'écran le dit, et le
            // bandeau hors-ligne prend le relais (D-077).
            setNote(t('new.queued'));
          } else {
            setNote(t('new.created', { code: result.code }));
          }

          router.back();
        },
        onError: (error) => {
          void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error);
          setNote(describeActionError(error, t));
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
          onPress={() => (step === 1 ? router.back() : setStep(step - 1))}
        >
          <ArrowLeft size={24} className="text-foreground" />
        </Pressable>
        <View className="flex-1">
          <Text className="text-[20px] font-bold text-foreground">{t('new.title')}</Text>
          <Text className="text-[14px] text-muted-foreground">{t('new.step', { n: step })}</Text>
        </View>
      </View>

      {/* La progression, en trois traits : plus lisible qu'un compteur seul. */}
      <View className="flex-row gap-1.5 px-5 pb-2">
        {[1, 2, 3].map((index) => (
          <View
            key={index}
            className={`h-1 flex-1 rounded-full ${index <= step ? 'bg-primary' : 'bg-muted'}`}
          />
        ))}
      </View>

      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        className="flex-1"
      >
        <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-4 px-5 pb-6">
          {step === 1 ? (
            <>
              <Field
                label={t('new.name')}
                value={form.recipientName}
                onChangeText={(value) => set('recipientName', value)}
                autoCapitalize="words"
              />
              <Field
                label={t('new.phone')}
                value={form.recipientPhone}
                onChangeText={(value) => set('recipientPhone', value)}
                placeholder={t('new.phoneHint')}
                hint={tooManyPhones ? t('new.too-many-phones') : t('new.phonesHint')}
                // Le pavé téléphonique reste : il porte la virgule (« pause »
                // sur iOS) qui sépare deux numéros, et « / » sur Android.
                inputMode="tel"
                autoCorrect={false}
              />
              <Field
                label={t('new.description')}
                value={form.description}
                onChangeText={(value) => set('description', value)}
              />
            </>
          ) : step === 2 ? (
            <>
              {/*
                Ville et adresse de la fiche, en LECTURE — point 9. Elles
                restent dans `form`, donc dans ce qui part au serveur et dans le
                calcul du tarif ; elles ne se saisissent plus.
              */}
              {fromRecord ? (
                <Card className="gap-1">
                  <Text className="text-[14px] font-medium text-muted-foreground">
                    {t('new.fromRecord')}
                  </Text>
                  <Text className="text-[16px] font-semibold text-foreground">
                    {company.data?.cityName}
                  </Text>
                  <Text className="text-[15px] text-muted-foreground">{recordAddress}</Text>
                  <Text className="text-[13px] text-muted-foreground">
                    {t('new.fromRecordHint')}
                  </Text>
                </Card>
              ) : (
                <>
                  <CitySelect
                    label={t('new.fromCity')}
                    cities={cities.data ?? []}
                    value={form.originCityId}
                    onChange={(id) => set('originCityId', id)}
                    placeholder={t('new.choose')}
                  />
                  <Field
                    label={t('new.pickupAddress')}
                    value={form.pickupAddress}
                    onChangeText={(value) => set('pickupAddress', value)}
                  />
                </>
              )}

              {/*
                L'option n'a de sens qu'une fois la fiche connue : sans elle,
                il n'y a rien à quoi revenir en la décochant.
              */}
              {company.data ? (
                <Toggle
                  label={t('new.pickupElsewhere')}
                  value={elsewhere}
                  onChange={setElsewhereChoice}
                />
              ) : null}

              <CitySelect
                label={t('new.toCity')}
                cities={cities.data ?? []}
                value={form.destinationCityId}
                // Le quartier saisi reste : c'est un texte, que le serveur
                // rapproche des quartiers de la ville finalement choisie.
                onChange={(id) => set('destinationCityId', id)}
                placeholder={t('new.choose')}
              />
              <DistrictField
                label={t('new.district')}
                placeholder={t('new.districtPlaceholder')}
                value={form.destinationDistrictName ?? ''}
                onChange={(value) => set('destinationDistrictName', value)}
                suggestions={districts.data ?? []}
              />
              <Field
                label={t('new.deliveryAddress')}
                value={form.deliveryAddress}
                onChangeText={(value) => set('deliveryAddress', value)}
              />
            </>
          ) : (
            <>
              <Field label={t('new.cod')} value={cod} onChangeText={setCod} inputMode="decimal" />

              {/* Recette 3 point 3 : Client Pro seulement, Espèces par défaut. */}
              {company.data?.isPro ? (
                <ExpectedPaymentChoice
                  value={form.expectedPayment ?? 'cash'}
                  onChange={(value) => set('expectedPayment', value)}
                />
              ) : null}

              <Card className="flex-row items-baseline justify-between gap-3">
                <Text className="text-[15px] text-muted-foreground">{t('new.fee')}</Text>
                <Text className="text-[17px] font-semibold tabular-nums text-foreground">
                  {quote.isLoading
                    ? t('new.feePending')
                    : quote.data
                      ? `${(quote.data.fee / 100).toFixed(2)} MAD`
                      : t('new.feeOffline')}
                </Text>
              </Card>

              <Toggle
                label={t('new.openOnDelivery')}
                value={form.openOnDelivery}
                onChange={(value) => set('openOnDelivery', value)}
              />
              <Toggle
                label={t('new.fragile')}
                value={form.fragile}
                onChange={(value) => set('fragile', value)}
              />

              {note ? <Text className="text-[15px] text-muted-foreground">{note}</Text> : null}
            </>
          )}
        </ScrollView>

        <View className="px-5 pb-4">
          {step < 3 ? (
            <Button
              label={t('new.next')}
              onPress={() => setStep(step + 1)}
              disabled={!stepReady}
              full
            />
          ) : (
            <Button
              label={t('new.create')}
              icon={Check}
              onPress={submit}
              busy={create.isPending}
              full
            />
          )}
        </View>
      </KeyboardAvoidingView>
    </Screen>
  );
}

/**
 * LE QUARTIER, EN SAISIE LIBRE — chantier « Quartier en saisie libre ».
 *
 * Un champ texte facultatif ; les quartiers connus de la ville qui commencent
 * comme la saisie sont proposés en pastilles, et un appui les recopie. Rien
 * n'est imposé : le serveur rapproche le texte du référentiel.
 */
function DistrictField({
  label,
  placeholder,
  value,
  onChange,
  suggestions,
}: {
  label: string;
  placeholder: string;
  value: string;
  onChange: (value: string) => void;
  suggestions: Choice[];
}) {
  const key = districtKey(value);
  const matches = suggestions
    .filter((option) => {
      const candidate = districtKey(option.name);
      return candidate !== key && (key === '' || candidate.includes(key));
    })
    .slice(0, 12);

  return (
    <View className="gap-2">
      <Field
        label={label}
        placeholder={placeholder}
        value={value}
        onChangeText={onChange}
        maxLength={120}
        autoCorrect={false}
      />
      {matches.length > 0 ? (
        <ScrollView
          keyboardShouldPersistTaps="handled"
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerClassName="gap-2"
        >
          {matches.map((option) => (
            <Chip
              key={option.id}
              label={option.name}
              active={false}
              onPress={() => onChange(option.name)}
            />
          ))}
        </ScrollView>
      ) : null}
    </View>
  );
}

function Toggle({
  label,
  value,
  onChange,
}: {
  label: string;
  value: boolean;
  onChange: (next: boolean) => void;
}) {
  return (
    <Card className="flex-row items-center justify-between gap-3">
      <Text className="flex-1 text-[16px] text-foreground">{label}</Text>
      <Switch value={value} onValueChange={onChange} />
    </Card>
  );
}

/** Coque Recette 7 (point 5) : le contenu repart vide à chaque fermeture — `useFreshKey`. */
export default function NewParcelScreen() {
  return <NewParcelScreenContent key={useFreshKey()} />;
}
