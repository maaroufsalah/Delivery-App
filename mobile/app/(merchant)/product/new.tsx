import { useRouter } from 'expo-router';
import { useFreshKey } from '@/lib/use-fresh-key';
import { useState } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { useCreateProduct } from '@/lib/merchant';
import { Button, Field, Screen, ScreenHeader } from '@/ui';

/**
 * AJOUTER UN PRODUIT — V2 vendeur bloc 7, spécification §9 et §16.
 *
 * **Trois champs, et c'est délibéré.** La fiche complète en porte une dizaine —
 * dimensions, poids, valeur déclarée, attributs, image — et se remplit au
 * bureau, sur le web. Ce que le téléphone sert, c'est le geste du dépôt : on
 * déballe un carton, on ajoute la référence au catalogue, on y revient plus
 * tard. Demander dix champs debout ferait abandonner à la moitié.
 *
 * **Le `clientId` est posé par le SERVEUR.** `resolveClient()` rend celui de la
 * portée sans regarder le corps pour un compte vendeur : c'est la même règle
 * que la création d'un colis, et la raison pour laquelle aucun choix de vendeur
 * n'apparaît ici (règle d'Anass).
 */
function MerchantNewProductContent() {
  const { t } = useI18n();
  const router = useRouter();

  const [name, setName] = useState('');
  const [reference, setReference] = useState('');
  const [price, setPrice] = useState('');
  const [error, setError] = useState<string | null>(null);

  const create = useCreateProduct();

  const submit = () => {
    setError(null);

    create.mutate(
      {
        name: name.trim(),
        reference: reference.trim() === '' ? undefined : reference.trim(),
        /*
         * Le prix voyage en CENTIMES, comme partout (DOMAIN.md §4 dette n°2).
         * `Math.round` et non une troncature : 12,345 saisi à la main doit
         * valoir 12,35 et non 12,34.
         */
        price: price.trim() === '' ? null : Math.round(Number(price.replace(',', '.')) * 100),
      },
      {
        onSuccess: (product) => {
          router.replace(`/(merchant)/product/${encodeURIComponent(product.id)}`);
        },
        onError: (cause) => setError((cause as Error).message),
      },
    );
  };

  const valid =
    name.trim().length >= 2 &&
    (price.trim() === '' || !Number.isNaN(Number(price.replace(',', '.'))));

  return (
    <Screen>
      <ScreenHeader title={t('warehouse.newProduct')} />

      <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-3 px-5 pb-8">
        <Field label={t('warehouse.productName')} value={name} onChangeText={setName} />
        <Field
          label={t('warehouse.productReference')}
          value={reference}
          onChangeText={setReference}
        />
        <Field
          label={t('warehouse.productPrice')}
          value={price}
          onChangeText={setPrice}
          keyboardType="decimal-pad"
        />

        {error ? <Text className="text-[15px] text-destructive">{error}</Text> : null}

        <View className="pt-2">
          <Button
            label={t('warehouse.createProduct')}
            onPress={submit}
            disabled={!valid || create.isPending}
            full
          />
        </View>
      </ScrollView>
    </Screen>
  );
}

/** Coque Recette 7 (point 5) : le contenu repart vide à chaque fermeture — `useFreshKey`. */
export default function MerchantNewProduct() {
  return <MerchantNewProductContent key={useFreshKey()} />;
}
