import { type MerchantCompany, phoneSchema } from '@delivery/shared';
import { useRouter } from 'expo-router';
import { useFreshKey } from '@/lib/use-fresh-key';
import { useState } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { CitySelect } from '@/components/city-select';
import { useCities, useCompany, useUpdateCompany } from '@/lib/merchant';
import { Button, Field, Screen, ScreenHeader, SkeletonCard } from '@/ui';

/**
 * MA SOCIÉTÉ — V2 vendeur bloc 9, spécification §7.
 *
 * **Ce que l'écran répare.** Ces champs n'étaient éditables que par
 * l'administration : un vendeur qui déménageait devait téléphoner à son
 * agence, et ses bons de ramassage partaient à l'ancienne adresse en
 * attendant.
 *
 * **Quatre champs, et pas un de plus.** Le régime de facturation, le compte
 * bancaire et l'agence de rattachement restent à l'administration : ce sont des
 * termes commerciaux négociés, pas des coordonnées. Les ouvrir laisserait un
 * vendeur passer seul en « professionnel », c'est-à-dire changer la façon dont
 * son argent lui revient (D-061).
 *
 * **L'e-mail est affiché, pas modifiable.** Il identifie le COMPTE autant que
 * la fiche, et le changer demande de vérifier la nouvelle adresse — c'est le
 * travail de « Mon profil », pas celui de cet écran.
 *
 * **Le formulaire est un composant SÉPARÉ, monté une fois la fiche arrivée.**
 * Remplir des champs depuis un effet aurait écrasé la saisie en cours au
 * premier rafraîchissement en arrière-plan — et la règle du compilateur React
 * le dit avant l'utilisateur.
 */
function MerchantCompanyScreenContent() {
  const { t } = useI18n();
  const company = useCompany();

  return (
    <Screen>
      <ScreenHeader title={t('company.title')} />

      {company.isLoading || !company.data ? (
        <View className="gap-3 px-5">
          <SkeletonCard />
          <SkeletonCard />
        </View>
      ) : (
        <CompanyForm company={company.data} />
      )}
    </Screen>
  );
}

/** Les champs, initialisés UNE fois depuis la fiche reçue — aucun effet. */
function CompanyForm({ company }: { company: MerchantCompany }) {
  const { t } = useI18n();
  const router = useRouter();

  const cities = useCities();
  const save = useUpdateCompany();

  const [name, setName] = useState(company.name);
  const [phone, setPhone] = useState(company.phone);
  const [cityId, setCityId] = useState(company.cityId);
  const [address, setAddress] = useState(company.address ?? '');
  const [error, setError] = useState<string | null>(null);

  const submit = () => {
    setError(null);

    save.mutate(
      { name: name.trim(), phone: phone.trim(), cityId, address: address.trim() },
      {
        onSuccess: () => router.back(),
        onError: (cause) => setError((cause as Error).message),
      },
    );
  };

  // Lot D, D1 (D-188) : le validateur partagé du serveur, chiffres et « + », tel que saisi.
  const phoneOk = phoneSchema.safeParse(phone.trim()).success;
  const valid = name.trim().length >= 2 && phoneOk && cityId !== '';

  return (
    <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-3 px-5 pb-8">
      <Field label={t('company.name')} value={name} onChangeText={setName} />
      <Field
        label={t('company.phone')}
        value={phone}
        onChangeText={setPhone}
        keyboardType="phone-pad"
        hint={phone.trim() !== '' && !phoneOk ? t('company.phoneHint') : undefined}
      />

      <CitySelect
        label={t('company.city')}
        cities={cities.data ?? []}
        value={cityId}
        onChange={setCityId}
        placeholder={t('new.choose')}
      />

      <Field label={t('company.address')} value={address} onChangeText={setAddress} multiline />

      {/* L'e-mail se lit, il ne se change pas ici : il identifie le compte
          autant que la fiche. */}
      <View className="gap-0.5">
        <Text className="text-[15px] text-muted-foreground">{t('company.email')}</Text>
        <Text className="text-[16px] text-foreground">{company.email}</Text>
      </View>

      {error ? <Text className="text-[15px] text-destructive">{error}</Text> : null}

      <View className="pt-2">
        <Button
          label={t('common.save')}
          onPress={submit}
          disabled={!valid || save.isPending}
          full
        />
      </View>
    </ScrollView>
  );
}

/** Coque Recette 7 (point 5) : le contenu repart vide à chaque fermeture — `useFreshKey`. */
export default function MerchantCompanyScreen() {
  return <MerchantCompanyScreenContent key={useFreshKey()} />;
}
