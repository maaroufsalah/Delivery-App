import { useRouter } from 'expo-router';
import { ArrowLeft, Check, PackageCheck } from 'lucide-react-native';
import { useState } from 'react';
import { FlatList, Pressable, RefreshControl, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n } from '@/i18n';
import { describeActionError } from '@/lib/use-action';
import { can, useSession } from '@/lib/session';
import { usePendingNotes, useValidateNote } from '@/lib/staff';
import { Button, Card, EmptyState, Screen, SkeletonCard } from '@/ui';

/**
 * RAMASSAGES À VALIDER — M3 bloc 8.
 *
 * Le pendant de la demande du vendeur (D-082). Valider, c'est **attester** que
 * l'agence est passée prendre la marchandise : les colis passent en « ramassé »,
 * et c'est ce geste que le demandeur ne peut pas faire lui-même.
 *
 * **Le bouton n'apparaît pas sans `notes.write`.** La matrice est éditable
 * (D-019) : un agent de comptoir peut avoir le droit de voir la file sans celui
 * de la solder. Lui montrer un bouton qui rendra 403 serait pire que de ne rien
 * lui montrer.
 */
export default function StaffPickupsScreen() {
  const { t } = useI18n();
  const router = useRouter();
  const { profile } = useSession();

  const { data, isLoading, refetch } = usePendingNotes();
  const pull = usePullToRefresh({ refetch });
  const validate = useValidateNote();

  const [note, setNote] = useState<string | null>(null);
  const mayValidate = can(profile, 'notes.write');

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
        <Text className="flex-1 text-[20px] font-bold text-foreground">
          {t('staff.pendingNotes')}
        </Text>
      </View>

      {isLoading ? (
        <View className="gap-2 px-5">
          <SkeletonCard />
          <SkeletonCard />
        </View>
      ) : (
        <FlatList
          keyboardShouldPersistTaps="handled"
          data={data?.items ?? []}
          keyExtractor={(item) => item.id}
          contentContainerClassName="gap-2 px-5 pb-6"
          refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
          ListEmptyComponent={<EmptyState icon={PackageCheck} title={t('staff.noPickups')} />}
          renderItem={({ item }) => (
            <Card className="gap-2">
              <Text className="font-mono text-[15px] text-muted-foreground">{item.code}</Text>
              <Text className="text-[16px] font-semibold text-foreground">{item.clientName}</Text>
              <Text className="text-[15px] text-muted-foreground">
                {item.cityName} · {item.parcelCount}
              </Text>

              {mayValidate ? (
                <Button
                  label={t('staff.validate')}
                  icon={Check}
                  busy={validate.isPending}
                  onPress={() =>
                    validate.mutate(item.code, {
                      onSuccess: () => setNote(t('staff.validated', { code: item.code })),
                      onError: (error) => setNote(describeActionError(error, t)),
                    })
                  }
                  full
                />
              ) : null}
            </Card>
          )}
        />
      )}

      {note ? <Text className="px-5 pb-4 text-[15px] text-muted-foreground">{note}</Text> : null}
    </Screen>
  );
}
