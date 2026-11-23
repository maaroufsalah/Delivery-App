import { useLocalSearchParams, useRouter } from 'expo-router';
import { useFreshKey } from '@/lib/use-fresh-key';
import { PackageCheck, Truck } from 'lucide-react-native';
import { useEffect, useState } from 'react';
import { FlatList, Pressable, RefreshControl, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n } from '@/i18n';
import { describeActionError } from '@/lib/use-action';
import type { NoteKind } from '@delivery/shared';
import { useNoteCandidates, useRequestNote } from '@/lib/merchant';
import { Button, Card, EmptyState, Screen, Skeleton } from '@/ui';

/**
 * DEMANDER UN BON — M2 bloc 6, étendu au DÉPÔT au V2 vendeur bloc 4.
 *
 * On coche, on demande. Le bon naît EN ATTENTE : c'est l'agence qui atteste
 * être passée prendre la marchandise, et cette asymétrie est le cœur de la
 * route qui sert cet écran — un vendeur ne peut pas se déclarer ramassé
 * lui-même.
 *
 * **Est coché par défaut ce qui est déjà prévu dans ce mode.** Le cas courant
 * est « venez chercher tout ce que j'ai préparé » ; décocher une exception est
 * plus rapide que cocher quinze lignes. Les colis prévus dans l'autre mode sont
 * proposés décochés (recette 4, point 2).
 */
function PickupScreenContent() {
  const { t } = useI18n();
  const router = useRouter();

  /*
   * LE MÊME ÉCRAN POUR LES DEUX GENRES — V2 vendeur bloc 4.
   *
   * Le geste est identique : on coche, on demande, le bon naît en attente. Ce
   * qui change est le statut des colis proposés et l'endpoint appelé. En
   * écrire deux aurait fait deux listes à cocher à tenir d'accord, pour une
   * différence d'un mot.
   *
   * `pickup` par défaut : c'est le chemin d'origine, et l'entrée sans
   * paramètre qui existait avant ce bloc.
   */
  const { kind: raw } = useLocalSearchParams<{ kind?: string }>();
  const kind: NoteKind = raw === 'dropoff' ? 'dropoff' : 'pickup';

  const { data, isLoading, refetch, hasNextPage, isFetchingNextPage, fetchNextPage } =
    useNoteCandidates(kind);
  const pull = usePullToRefresh({ refetch });

  /*
   * TOUTES LES PAGES, ENCHAÎNÉES — chantier « Plafond de 200 ». Le compteur du
   * bouton doit compter les 260 colis cochés, pas les cent premiers : le bouton
   * reste désactivé tant que la liste n'est pas entière.
   */
  useEffect(() => {
    if (hasNextPage && !isFetchingNextPage) void fetchNextPage();
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  const total = data?.pages[0]?.total ?? 0;
  const complete = !hasNextPage;
  const request = useRequestNote(kind);

  /*
   * COCHÉS PAR DÉFAUT : les colis déjà prévus dans CE mode — recette 4, point 2.
   *
   * La liste porte désormais tous les colis pas encore pris en charge (D-132),
   * ramassage et dépôt confondus. Tout cocher d'office aurait fait basculer
   * sans le dire les colis prévus dans l'autre mode. Ils restent proposés,
   * décochés, et disent ce qu'il adviendra d'eux si on les coche.
   */
  const modeStatusId = kind === 'pickup' ? 1 : 2;
  const sameMode = (parcel: { statusId: number }) => parcel.statusId === modeStatusId;
  /** Les colis dont le choix par défaut a été INVERSÉ par le vendeur. */
  const [flipped, setFlipped] = useState<Set<string>>(new Set());
  const [note, setNote] = useState<string | null>(null);

  const parcels = data?.pages.flatMap((page) => page.items) ?? [];
  const isPicked = (parcel: { id: string; statusId: number }) =>
    sameMode(parcel) !== flipped.has(parcel.id);
  const chosen = parcels.filter(isPicked);

  const toggle = (id: string) =>
    setFlipped((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const submit = () => {
    request.mutate(
      chosen.map((parcel) => parcel.id),
      {
        onSuccess: (result) => {
          setNote(t('pickup.done', { code: result.code }));
          setFlipped(new Set());
          router.back();
        },
        onError: (error) => setNote(describeActionError(error, t)),
      },
    );
  };

  if (isLoading) {
    return (
      <Screen className="gap-2 px-5 pt-4">
        <Skeleton className="h-16" />
        <Skeleton className="h-16" />
        <Skeleton className="h-16" />
      </Screen>
    );
  }

  return (
    <Screen>
      <View className="gap-1 px-5 pb-2 pt-4">
        <Text className="text-[22px] font-bold text-foreground">
          {kind === 'pickup' ? t('pickup.title') : t('notes.newDropoff')}
        </Text>
        <Text className="text-[15px] text-muted-foreground">
          {kind === 'pickup' ? t('pickup.hint') : t('notes.newDropoffHint')}
        </Text>
      </View>

      <FlatList
        keyboardShouldPersistTaps="handled"
        data={parcels}
        keyExtractor={(item) => item.id}
        contentContainerClassName="gap-2 px-5 pb-4"
        refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
        ListEmptyComponent={
          <EmptyState icon={PackageCheck} title={t('pickup.empty')} hint={t('pickup.emptyHint')} />
        }
        renderItem={({ item }) => {
          const picked = isPicked(item);

          return (
            <Pressable
              accessibilityRole="checkbox"
              hitSlop={6}
              accessibilityState={{ checked: picked }}
              onPress={() => toggle(item.id)}
            >
              <Card
                className={`flex-row items-center gap-3 ${picked ? 'border border-primary' : ''}`}
              >
                <View
                  className={`size-6 items-center justify-center rounded-md border ${
                    picked ? 'border-primary bg-primary' : 'border-border'
                  }`}
                >
                  {picked ? <PackageCheck size={14} className="text-primary-foreground" /> : null}
                </View>

                <View className="flex-1">
                  <Text className="font-mono text-[14px] text-muted-foreground">{item.code}</Text>
                  <Text className="text-[16px] text-foreground">{item.recipientName}</Text>
                  <Text className="text-[14px] text-muted-foreground">
                    {item.destinationCityName}
                  </Text>
                  {sameMode(item) ? null : (
                    <Text className="text-[13px] text-muted-foreground">
                      {kind === 'pickup' ? t('pickup.switchToPickup') : t('pickup.switchToDropoff')}
                    </Text>
                  )}
                </View>
              </Card>
            </Pressable>
          );
        }}
      />

      {note ? <Text className="px-5 pb-2 text-[15px] text-muted-foreground">{note}</Text> : null}

      {parcels.length > 0 ? (
        <View className="gap-2 px-5 pb-4">
          {complete ? null : (
            <Text className="text-center text-[14px] text-muted-foreground">
              {t('pickup.loading', { shown: String(parcels.length), total: String(total) })}
            </Text>
          )}
          <Button
            label={t('pickup.request', { count: chosen.length })}
            icon={Truck}
            onPress={submit}
            disabled={chosen.length === 0 || !complete}
            busy={request.isPending}
            full
          />
        </View>
      ) : null}
    </Screen>
  );
}

/** Coque Recette 7 (point 5) : le contenu repart vide à chaque `kind` et à chaque fermeture — `useFreshKey`. */
export default function PickupScreen() {
  const { kind } = useLocalSearchParams<{ kind?: string }>();
  return <PickupScreenContent key={useFreshKey(kind)} />;
}
