import { findParcelStatus, isCourierReturnStatus } from '@delivery/shared';
import { useRouter } from 'expo-router';
import { useFreshKey } from '@/lib/use-fresh-key';
import { Camera, Check, PackageX, Search } from 'lucide-react-native';
import { useMemo, useState } from 'react';
import { Pressable, ScrollView, Text, TextInput, View } from 'react-native';
import { CartonTag, CartonTotal } from '@/components/carton-count';
import { useI18n } from '@/i18n';
import { describeActionError } from '@/lib/use-action';
import { useHandoverActions, useHandoverCandidates } from '@/lib/courier';
import {
  Button,
  Card,
  EmptyState,
  Screen,
  ScreenHeader,
  SkeletonCard,
  StatusBadge,
  useToast,
} from '@/ui';

/**
 * LE LIVREUR OUVRE SON BON DE RETOUR — chantier « Bon de retour livreur »,
 * point 1.
 *
 * Trois écrans en un, et l'ordre est celui du geste :
 *
 *  1. **ses colis rapportables**, servis par le serveur avec les MÊMES règles
 *     que l'assistant de l'admin — ni ramassé, ni ramassé par le livreur, ni
 *     livré, ni en cours de livraison, ni déjà réservé par un bon actif. Le
 *     téléphone n'en filtre aucun : il n'a pas à connaître la règle ;
 *  2. **le récapitulatif**, parce que cocher douze lignes dans une liste qui
 *     défile ne dit pas ce qu'on s'apprête à signer ;
 *  3. **la création**, qui rend un `RL-` en « Retour demandé », origine
 *     LIVREUR. Pas de brouillon de ce côté : quand il valide, il a déjà les
 *     cartons en main.
 *
 * **La recherche prend le code ET la douchette.** Le champ est le même : un
 * code scanné arrive comme un code tapé, et la sélection suit. C'est la
 * mécanique du scan de ramassage, sans caméra à ouvrir pour un seul carton —
 * le bouton la propose quand la liste est longue.
 */
function NewHandoverContent() {
  const { t, status } = useI18n();
  const router = useRouter();
  const toast = useToast();

  const candidates = useHandoverCandidates();
  const { create } = useHandoverActions();

  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<string[]>([]);
  const [step, setStep] = useState<'pick' | 'review'>('pick');

  // Le serveur filtre déjà (D-153) ; on refiltre pour qu’une API plus ancienne ne propose rien d’autre.
  const rows = (candidates.data ?? []).filter((parcel) => isCourierReturnStatus(parcel.statusId));

  const visible = useMemo(() => {
    const needle = search.trim().toLowerCase();
    if (!needle) return rows;

    return rows.filter(
      (parcel) =>
        parcel.code.toLowerCase().includes(needle) ||
        (parcel.externalCode ?? '').toLowerCase().includes(needle) ||
        parcel.recipientName.toLowerCase().includes(needle) ||
        parcel.recipientPhone.toLowerCase().includes(needle) ||
        parcel.clientName.toLowerCase().includes(needle),
    );
  }, [rows, search]);

  const chosen = rows.filter((parcel) => selected.includes(parcel.id));

  const toggle = (id: string) =>
    setSelected((current) =>
      current.includes(id) ? current.filter((entry) => entry !== id) : [...current, id],
    );

  if (candidates.isLoading) {
    return (
      <Screen>
        <ScreenHeader title={t('handovers.new')} backHref={'/(courier)/returns'} />
        <View className="gap-3 px-5">
          <SkeletonCard />
          <SkeletonCard />
        </View>
      </Screen>
    );
  }

  /* --- Étape 2 : le récapitulatif ---------------------------------------- */
  if (step === 'review') {
    return (
      <Screen>
        <ScreenHeader title={t('handovers.review')} onBack={() => setStep('pick')} />

        <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-3 px-5 pb-8">
          <Text className="text-[13px] text-muted-foreground">{t('handovers.reviewHint')}</Text>
          {/* « 3 colis · 6 cartons » — ce que le bon emportera. */}
          <CartonTotal lines={chosen} />

          {chosen.map((parcel) => (
            <Card key={parcel.id} className="gap-1">
              <Text className="font-mono text-[15px] font-bold text-foreground">{parcel.code}</Text>
              <Text className="text-[13px] text-muted-foreground" numberOfLines={1}>
                {parcel.recipientName} · {parcel.clientName}
              </Text>
              <CartonTag line={parcel} />
            </Card>
          ))}

          <Button
            label={t('handovers.parcels', { count: chosen.length })}
            tone="ghost"
            onPress={() => setStep('pick')}
          />

          <Button
            label={t('handovers.create')}
            icon={Check}
            busy={create.isPending}
            onPress={() => {
              // Aucun identifiant : le serveur lit sa fiche (D-082).
              create.mutate(
                { parcelIds: selected },
                {
                  onSuccess: (handover) => {
                    toast.show(handover.code);
                    router.replace(`/(courier)/handover/${handover.code}`);
                  },
                  onError: (error: Error) => toast.show(describeActionError(error, t), 'error'),
                },
              );
            }}
          />
        </ScrollView>
      </Screen>
    );
  }

  /* --- Étape 1 : les colis ------------------------------------------------ */
  return (
    <Screen>
      <ScreenHeader title={t('handovers.new')} backHref={'/(courier)/returns'} />

      <View className="gap-2 px-5 pb-3">
        <View className="flex-row items-center gap-2 rounded-xl border border-border bg-card px-3">
          <Search size={18} className="text-muted-foreground" />
          <TextInput
            value={search}
            onChangeText={setSearch}
            placeholder={t('handovers.search')}
            placeholderTextColor="#9ca3af"
            autoCapitalize="characters"
            autoCorrect={false}
            className="min-h-11 flex-1 text-[15px] text-foreground"
          />
        </View>

        {/*
          LA DOUCHETTE, quand la liste est longue. Elle mène au scanner déjà
          en service (D-073) : un second lecteur de codes-barres dans
          l'application serait un second endroit où régler la caméra.
        */}
        <Button
          label={t('handovers.scan')}
          tone="ghost"
          icon={Camera}
          onPress={() => router.push('/(courier)/scan')}
        />
      </View>

      <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-3 px-5 pb-28">
        {/* « 5 colis · 9 cartons » — les colis proposés, cartons compris. */}
        <CartonTotal lines={rows} />
        {rows.length === 0 ? (
          <EmptyState
            art="box"
            icon={PackageX}
            title={t('handovers.noCandidates')}
            hint={t('handovers.noCandidatesHint')}
          />
        ) : (
          visible.map((parcel) => {
            const definition = findParcelStatus(parcel.statusId);
            const picked = selected.includes(parcel.id);

            return (
              <Pressable key={parcel.id} onPress={() => toggle(parcel.id)}>
                <Card className={`gap-2 ${picked ? 'border-primary' : ''}`}>
                  <View className="flex-row items-center justify-between gap-3">
                    <Text className="flex-1 font-mono text-[15px] font-bold text-foreground">
                      {parcel.code}
                    </Text>
                    {picked ? <Check size={18} className="text-primary" /> : null}
                  </View>

                  <Text className="text-[13px] text-muted-foreground" numberOfLines={1}>
                    {parcel.recipientName} · {parcel.clientName}
                  </Text>
                  <CartonTag line={parcel} />

                  {definition ? (
                    <View className="flex-row">
                      <StatusBadge label={status(definition.key)} color={definition.color} />
                    </View>
                  ) : null}
                </Card>
              </Pressable>
            );
          })
        )}
      </ScrollView>

      {/*
        LA BARRE DE VALIDATION, collée en bas : le pouce y est déjà, et le
        compte de ce qu'on emporte reste visible pendant qu'on coche.
      */}
      {selected.length > 0 ? (
        <View className="absolute bottom-0 end-0 start-0 border-t border-border bg-background p-4">
          <Button
            label={t('handovers.reviewCount', { count: selected.length })}
            onPress={() => setStep('review')}
          />
        </View>
      ) : null}
    </Screen>
  );
}

/** Coque Recette 7 (point 5) : le contenu repart vide à chaque fermeture — `useFreshKey`. */
export default function NewHandover() {
  return <NewHandoverContent key={useFreshKey()} />;
}
