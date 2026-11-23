import { allowedTransitions, getParcelStatus, isLockedStatus } from '@delivery/shared';
import * as Haptics from 'expo-haptics';
import { useLocalSearchParams } from 'expo-router';
import { useFreshKey } from '@/lib/use-fresh-key';
import { ArrowLeft, NotebookPen, Repeat } from 'lucide-react-native';
import { useState } from 'react';
import { Modal, Pressable, ScrollView, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useGoBack } from '@/lib/go-back';
import { useI18n } from '@/i18n';
import { useAddNote, useChangeStatus, useParcel } from '@/lib/staff';
import { formatStamp } from '@/lib/datetime';
import { Button, Card, Field, Screen, Skeleton, SkeletonCard, StatusBadge, Timeline } from '@/ui';

/**
 * FICHE COLIS, côté AGENCE — M3 bloc 7.
 *
 * **Les transitions viennent de la machine à états**, pas d'une liste écrite
 * ici : `allowedTransitions(statusId)` est la même fonction que le back-office
 * utilise, et que l'API applique à la réception. Trois listes qui disent la
 * même chose finiraient par ne plus la dire — c'est ce que le legacy faisait,
 * et il autorisait 39 sorties d'un statut terminal (DOMAIN.md §2.2).
 *
 * **Un statut VERROUILLÉ ne se change pas** : livré et retour remis sont des
 * fins de course, et en sortir demande une contestation motivée, qui est un
 * geste de back-office. L'écran le dit plutôt que de proposer une feuille vide.
 *
 * **La note d'agence est ici, et le commentaire de statut aussi** — mais ils ne
 * font pas la même chose. Le commentaire s'attache à l'ÉVÉNEMENT et voyage avec
 * lui ; la note vit à part, et ne descend jamais chez l'e-commerçant (M2 bloc 4).
 */
function StaffParcelScreenContent() {
  const { t, status, locale } = useI18n();
  // Une notification ouvre cette fiche sans rien derrière : on se rabat.
  const goBack = useGoBack('/(staff)/search');
  const { code } = useLocalSearchParams<{ code: string }>();

  const parcel = useParcel(code);
  const change = useChangeStatus(code ?? '');
  const note = useAddNote(code ?? '');

  const [sheet, setSheet] = useState(false);
  const [target, setTarget] = useState<number | null>(null);
  const [comment, setComment] = useState('');
  const [noteBody, setNoteBody] = useState('');

  const current = parcel.data ? getParcelStatus(parcel.data.statusId) : null;
  const locked = parcel.data ? isLockedStatus(parcel.data.statusId) : false;
  const targets = parcel.data ? allowedTransitions(parcel.data.statusId) : [];

  const apply = () => {
    if (target === null) return;

    change.mutate(
      { statusId: target, comment: comment.trim() || undefined },
      {
        onSuccess: () => {
          void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
          setSheet(false);
          setTarget(null);
          setComment('');
        },
        onError: () => void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error),
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
          onPress={goBack}
        >
          <ArrowLeft size={24} className="text-foreground" />
        </Pressable>
        <Text className="flex-1 font-mono text-[17px] text-foreground">{code}</Text>
        {current ? <StatusBadge label={status(current.key)} color={current.color} /> : null}
      </View>

      {parcel.isLoading ? (
        <View className="gap-3 px-5 pt-2">
          <SkeletonCard />
          <Skeleton className="h-40" />
        </View>
      ) : (
        <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-3 px-5 pb-8">
          <Card className="gap-1">
            <Text className="text-[18px] font-semibold text-foreground">
              {parcel.data?.recipientName}
            </Text>
            <Text className="text-[15px] text-muted-foreground">{parcel.data?.recipientPhone}</Text>
            <Text className="text-[15px] text-muted-foreground">
              {parcel.data?.deliveryAddress}
            </Text>
            <Text className="text-[15px] text-muted-foreground">
              {parcel.data?.destinationCityName}
              {parcel.data?.destinationDistrictName
                ? ` · ${parcel.data.destinationDistrictName}`
                : ''}
            </Text>
            <Text className="pt-1 text-[15px] text-foreground">{parcel.data?.clientName}</Text>
          </Card>

          {/* --- L'historique, complet : l'agence a le droit de tout voir --- */}
          <Text className="pt-1 text-[18px] font-semibold text-foreground">
            {t('staff.history')}
          </Text>

          {/*
            LA TIMELINE, et non plus une carte par événement — M5.

            Un historique est une suite, pas une collection : le fil et les
            pastilles disent l'ordre et l'état courant d'un coup d'œil, là où
            huit cartes empilées demandent de lire les dates pour comprendre
            laquelle est la dernière. C'est le composant du livreur (M4), et
            l'agence voit la même chose que lui — avec, en plus, ce que lui ne
            voit pas : l'auteur de chaque changement.
          */}
          <Card>
            <Timeline
              entries={(parcel.data?.events ?? []).map((event) => {
                const definition = getParcelStatus(event.statusId);

                return {
                  id: event.id,
                  label: status(definition.key),
                  color: definition.color,
                  at: formatStamp(event.createdAt, locale),
                  author: event.authorName,
                  comment: event.comment,
                };
              })}
            />
          </Card>

          {/* --- Les notes d'agence --- */}
          <Text className="pt-1 text-[18px] font-semibold text-foreground">{t('staff.notes')}</Text>

          {(parcel.data?.notes ?? []).length === 0 ? (
            <Text className="px-1 text-[15px] text-muted-foreground">{t('staff.noNotes')}</Text>
          ) : (
            (parcel.data?.notes ?? []).map((row) => (
              <Card key={row.id} className="gap-1">
                <Text className="text-[15px] text-foreground">{row.body}</Text>
                <Text className="text-[13px] text-muted-foreground">
                  {row.authorName} · {formatStamp(row.createdAt, locale)}
                </Text>
              </Card>
            ))
          )}

          <Field label={t('staff.addNote')} value={noteBody} onChangeText={setNoteBody} multiline />
          <Text className="px-1 text-[13px] text-muted-foreground">{t('staff.noteHint')}</Text>
          <Button
            label={t('staff.addNote')}
            icon={NotebookPen}
            tone="ghost"
            disabled={noteBody.trim().length === 0}
            busy={note.isPending}
            onPress={() => note.mutate(noteBody.trim(), { onSuccess: () => setNoteBody('') })}
            full
          />
        </ScrollView>
      )}

      {/* --- Le geste principal, en bas, sous le pouce --- */}
      <View className="px-5 pb-4">
        {locked ? (
          <Text className="py-3 text-center text-[15px] text-muted-foreground">
            {t('staff.locked')}
          </Text>
        ) : (
          <Button
            label={t('staff.changeStatus')}
            icon={Repeat}
            onPress={() => setSheet(true)}
            disabled={targets.length === 0}
            full
          />
        )}
      </View>

      {/* --- La feuille des transitions --- */}
      <Modal visible={sheet} animationType="slide" onRequestClose={() => setSheet(false)}>
        {/* `bottom` en plus : cet écran porte une barre d'action collée au
            bord, là où les autres laissent la barre d'onglets s'en charger. */}
        <SafeAreaView className="flex-1 bg-background" edges={['top', 'bottom']}>
          <View className="flex-row items-center gap-3 px-5 py-3">
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={t('common.close')}
              className="min-h-11 min-w-11 items-center justify-center"
              hitSlop={8}
              onPress={() => setSheet(false)}
            >
              <ArrowLeft size={24} className="text-foreground" />
            </Pressable>
            <Text className="flex-1 text-[20px] font-bold text-foreground">
              {t('staff.changeStatus')}
            </Text>
          </View>

          <ScrollView
            keyboardShouldPersistTaps="handled"
            contentContainerClassName="gap-2 px-5 pb-4"
          >
            {targets.map((statusId) => {
              const definition = getParcelStatus(statusId);

              return (
                <Pressable
                  key={statusId}
                  accessibilityRole="button"
                  accessibilityState={{ selected: target === statusId }}
                  onPress={() => setTarget(statusId)}
                >
                  <Card
                    className={`flex-row items-center gap-3 ${
                      target === statusId ? 'border border-primary' : ''
                    }`}
                  >
                    <View
                      className="size-3 rounded-full"
                      style={{ backgroundColor: definition.color }}
                    />
                    <Text className="flex-1 text-[16px] text-foreground">
                      {status(definition.key)}
                    </Text>
                  </Card>
                </Pressable>
              );
            })}

            <Field label={t('staff.comment')} value={comment} onChangeText={setComment} />
          </ScrollView>

          <View className="px-5 pb-4">
            <Button
              label={t('common.confirm')}
              onPress={apply}
              disabled={target === null}
              busy={change.isPending}
              full
            />
          </View>
        </SafeAreaView>
      </Modal>
    </Screen>
  );
}

/** Coque Recette 7 (point 5) : le contenu repart vide à chaque `code` et à chaque fermeture — `useFreshKey`. */
export default function StaffParcelScreen() {
  const { code } = useLocalSearchParams<{ code: string }>();
  return <StaffParcelScreenContent key={useFreshKey(code)} />;
}
