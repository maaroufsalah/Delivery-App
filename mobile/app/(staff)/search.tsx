import { getParcelStatus } from '@delivery/shared';
import { useRouter } from 'expo-router';
import { SearchX } from 'lucide-react-native';
import { useState } from 'react';
import { FlatList, Pressable, Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { useSearch } from '@/lib/staff';
import { Card, EmptyState, Field, Screen, SkeletonCard, StatusBadge } from '@/ui';

/**
 * LA RECHERCHE — M3 bloc 7.
 *
 * **Un seul champ.** Le serveur cherche déjà sur sept colonnes à la fois : code,
 * code externe, destinataire, téléphone, description, nom du client, ville.
 * Proposer des filtres ferait choisir un agent qui a un client au téléphone et
 * n'a le temps de taper qu'une chose — celle qu'il a sous les yeux.
 *
 * **Trois caractères au minimum.** En dessous, la requête ramènerait une
 * fraction de la base pour rien : `06` correspond à tous les numéros marocains.
 */
export default function StaffSearchScreen() {
  const { t, status } = useI18n();
  const router = useRouter();

  const [query, setQuery] = useState('');
  const { data, isLoading } = useSearch(query);

  const short = query.trim().length > 0 && query.trim().length < 3;

  return (
    <Screen>
      <View className="gap-3 px-5 pb-2 pt-4">
        <Text className="text-[22px] font-bold text-foreground">{t('staff.search')}</Text>

        <Field
          label={t('staff.search')}
          value={query}
          onChangeText={setQuery}
          placeholder={t('staff.searchHint')}
          autoCapitalize="characters"
          autoCorrect={false}
          inputMode="search"
          autoFocus
        />

        {short ? (
          <Text className="text-[14px] text-muted-foreground">{t('staff.searchShort')}</Text>
        ) : null}
      </View>

      {isLoading && !short ? (
        <View className="gap-2 px-5 pt-2">
          <SkeletonCard />
          <SkeletonCard />
        </View>
      ) : (
        <FlatList
          keyboardShouldPersistTaps="handled"
          data={data?.items ?? []}
          keyExtractor={(item) => item.id}
          contentContainerClassName="gap-2 px-5 pb-6"
          ListEmptyComponent={
            query.trim().length >= 3 ? (
              <EmptyState icon={SearchX} title={t('staff.noResult')} />
            ) : null
          }
          renderItem={({ item }) => {
            const definition = getParcelStatus(item.statusId);

            return (
              <Pressable
                accessibilityRole="button"
                onPress={() => router.push(`/(staff)/parcel/${encodeURIComponent(item.code)}`)}
              >
                <Card className="gap-2">
                  <View className="flex-row items-start justify-between gap-3">
                    <Text className="flex-1 font-mono text-[15px] text-muted-foreground">
                      {item.code}
                    </Text>
                    <StatusBadge label={status(definition.key)} color={definition.color} />
                  </View>

                  <Text className="text-[16px] font-semibold text-foreground">
                    {item.recipientName}
                  </Text>
                  <Text className="text-[14px] text-muted-foreground">
                    {item.recipientPhone} · {item.destinationCityName}
                  </Text>
                  <Text className="text-[14px] text-muted-foreground">{item.clientName}</Text>
                </Card>
              </Pressable>
            );
          }}
        />
      )}
    </Screen>
  );
}
