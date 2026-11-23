import { type CitySearchable, searchCities } from '@delivery/shared';
import { Check, ChevronDown } from 'lucide-react-native';
import { useMemo, useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { Field, Sheet } from '@/ui';

/** Ce qu'une ville doit porter pour être choisie et cherchée. */
export type CityChoice = CitySearchable & { id: string };

/**
 * LE SÉLECTEUR DE VILLE — chantier « Retours recette 26/09 », point 2.
 *
 * Le pendant mobile du `CitySelect` du web, même règle de recherche
 * (`searchCities`, sans casse ni accents, nom français, nom arabe et code).
 * Les pastilles d'avant tenaient pour « une quinzaine de villes » ; la grille
 * en compte 325, et une rangée qui défile sur 325 pastilles ne se parcourt
 * plus. Un champ qui ressemble aux autres, une feuille qui monte, un champ de
 * recherche en tête, et la liste qui se réduit à chaque frappe.
 *
 * La valeur émise reste l'`id` de la ville, exactement comme les pastilles.
 */
export function CitySelect({
  label,
  cities,
  value,
  onChange,
  placeholder,
}: {
  label: string;
  cities: readonly CityChoice[];
  value: string;
  onChange: (id: string) => void;
  placeholder: string;
}) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');

  const selected = useMemo(() => cities.find((city) => city.id === value), [cities, value]);
  const matches = useMemo(() => searchCities(cities, query), [cities, query]);

  const close = () => {
    setOpen(false);
    // La saisie ne survit pas à la fermeture : rouvrir montre la liste
    // entière, pas le résidu d'une recherche précédente.
    setQuery('');
  };

  return (
    <View className="gap-1.5">
      <Text className="text-[14px] font-medium text-muted-foreground">{label}</Text>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={label}
        accessibilityValue={{ text: selected?.name ?? placeholder }}
        onPress={() => setOpen(true)}
        // La même silhouette que `Field` : 52 points, coins et bordure.
        className="min-h-[52px] flex-row items-center justify-between gap-2 rounded-2xl border border-border bg-card px-4"
        style={({ pressed }) => (pressed ? { opacity: 0.7 } : null)}
      >
        <Text
          className={`flex-1 text-[16px] ${selected ? 'text-foreground' : 'text-muted-foreground'}`}
          numberOfLines={1}
        >
          {selected?.name ?? placeholder}
        </Text>
        <ChevronDown size={20} className="text-muted-foreground" />
      </Pressable>

      <Sheet open={open} onClose={close} title={label}>
        <View className="px-2 pb-2">
          <Field
            placeholder={t('city.search')}
            value={query}
            onChangeText={setQuery}
            autoFocus
            autoCorrect={false}
            autoCapitalize="none"
            returnKeyType="search"
          />
        </View>

        {matches.length === 0 ? (
          <Text className="px-5 py-4 text-[15px] text-muted-foreground">{t('city.empty')}</Text>
        ) : (
          matches.map((city) => {
            const active = city.id === value;
            return (
              <Pressable
                key={city.id}
                accessibilityRole="button"
                accessibilityState={{ selected: active }}
                onPress={() => {
                  close();
                  if (city.id !== value) onChange(city.id);
                }}
                className="min-h-[48px] flex-row items-center gap-3 rounded-2xl px-3"
                style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
              >
                <View className="w-5 items-center">
                  {active ? <Check size={18} className="text-primary" /> : null}
                </View>
                <Text
                  className={`flex-1 text-[16px] ${active ? 'font-semibold text-foreground' : 'text-foreground'}`}
                  numberOfLines={1}
                >
                  {city.name}
                </Text>
                {city.nameAr ? (
                  <Text className="text-[13px] text-muted-foreground" numberOfLines={1}>
                    {city.nameAr}
                  </Text>
                ) : null}
              </Pressable>
            );
          })
        )}
      </Sheet>
    </View>
  );
}
