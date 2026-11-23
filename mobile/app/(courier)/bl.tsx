import { BL_NOTE_STATUS_COLORS, type BlNoteStatus } from '@delivery/shared';
import { FileSignature, PackageCheck } from 'lucide-react-native';
import { useMemo } from 'react';
import { RefreshControl, ScrollView, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n, type MessageKey } from '@/i18n';
import { describeActionError } from '@/lib/use-action';
import { useBlNoteActions, useBlNotes, useCollectableSlips, useMe } from '@/lib/courier';
import {
  Button,
  Card,
  EmptyState,
  FadeIn,
  Screen,
  ScreenHeader,
  SectionTitle,
  SkeletonCard,
  StatusBadge,
  useToast,
} from '@/ui';

/**
 * LES BL DU LIVREUR — chantier « Retours, BL Clients Pro », point 5.
 *
 * L'écran répond à deux questions, dans cet ordre :
 *
 *  1. **Qu'est-ce que j'ai en sacoche ?** Les BL récupérés et pas encore
 *     groupés, rassemblés PAR VENDEUR — parce qu'un bon BL rassemble la liasse
 *     d'un vendeur, et qu'un livreur qui rentre avec trois vendeurs fait trois
 *     bons. Le regroupement est ici et pas au serveur : la liste est bornée à
 *     trois cents lignes et déjà chargée, et grouper une page paginée aurait
 *     groupé la page.
 *  2. **Où en sont mes liasses ?** Ses bons, avec leur état et, tant qu'ils
 *     sont en brouillon, le bouton qui les remet à l'entrepôt.
 *
 * **Ce que l'écran n'offre pas.** Ni réception, ni remise au vendeur : le rôle
 * `COURIER` ne porte pas `returns.confirm`, et celui qui rapporte les papiers
 * n'atteste pas lui-même les avoir rendus. Un bouton qui échoue toujours n'est
 * pas une sécurité, c'est une promesse fausse.
 */
export default function CourierBl() {
  const { t } = useI18n();
  const toast = useToast();

  const me = useMe();
  const slips = useCollectableSlips();
  const notes = useBlNotes();
  const pull = usePullToRefresh(slips, notes);
  const { create, handOver } = useBlNoteActions();

  /** Les BL en sacoche, par vendeur : un bon BL = une liasse = un vendeur. */
  const byClient = useMemo(() => {
    const groups = new Map<string, { clientId: string; clientName: string; ids: string[] }>();

    for (const slip of slips.data ?? []) {
      const group = groups.get(slip.clientId) ?? {
        clientId: slip.clientId,
        clientName: slip.clientName,
        ids: [],
      };

      group.ids.push(slip.id);
      groups.set(slip.clientId, group);
    }

    return [...groups.values()].sort((a, b) => a.clientName.localeCompare(b.clientName));
  }, [slips.data]);

  const openNotes = (notes.data?.items ?? []).filter((note) => note.status !== 'closed');
  const noteStatus = (status: BlNoteStatus): string => t(`bl.noteStatus.${status}` as MessageKey);

  if (slips.isLoading || notes.isLoading) {
    return (
      <Screen>
        <ScreenHeader title={t('bl.notes')} />
        <View className="gap-3 px-5">
          <SkeletonCard />
          <SkeletonCard />
        </View>
      </Screen>
    );
  }

  return (
    <Screen>
      <ScreenHeader title={t('bl.notes')} />

      <ScrollView
        keyboardShouldPersistTaps="handled"
        contentContainerClassName="gap-3 px-5 pb-8"
        refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
      >
        {byClient.length === 0 && openNotes.length === 0 ? (
          <EmptyState
            art="box"
            icon={FileSignature}
            title={t('bl.empty')}
            hint={t('bl.emptyHint')}
          />
        ) : null}

        {/* --- 1. Ce que j'ai en sacoche, par vendeur ------------------- */}
        {byClient.length > 0 ? (
          <>
            <SectionTitle label={t('bl.tracking')} />
            {byClient.map((group, index) => (
              <FadeIn key={group.clientId} index={index}>
                <Card className="gap-2">
                  <Text className="text-[15px] font-bold text-foreground">{group.clientName}</Text>
                  <Text className="text-[13px] text-muted-foreground">
                    {t('bl.slips', { count: group.ids.length })}
                  </Text>

                  <Button
                    label={t('bl.newNote')}
                    icon={FileSignature}
                    busy={create.isPending}
                    onPress={() => {
                      const agencyId = me.data?.agencyId;
                      if (!agencyId) return;

                      create.mutate(
                        { clientId: group.clientId, agencyId, slipIds: group.ids },
                        {
                          onSuccess: (note) => toast.show(note.code),
                          onError: (error: Error) =>
                            toast.show(describeActionError(error, t), 'error'),
                        },
                      );
                    }}
                  />
                </Card>
              </FadeIn>
            ))}
          </>
        ) : null}

        {/* --- 2. Où en sont mes liasses -------------------------------- */}
        {openNotes.length > 0 ? (
          <>
            <SectionTitle label={t('bl.notes')} />
            {openNotes.map((note, index) => (
              <FadeIn key={note.id} index={byClient.length + index}>
                <Card className="gap-2">
                  <View className="flex-row items-center justify-between gap-3">
                    <Text className="flex-1 font-mono text-[15px] font-bold text-foreground">
                      {note.code}
                    </Text>
                    <StatusBadge
                      label={noteStatus(note.status)}
                      color={BL_NOTE_STATUS_COLORS[note.status]}
                    />
                  </View>

                  <Text className="text-[13px] text-muted-foreground" numberOfLines={1}>
                    {note.clientName} · {t('bl.slips', { count: note.slipCount })}
                  </Text>

                  {note.status === 'draft' ? (
                    <Button
                      label={t('bl.handover')}
                      icon={PackageCheck}
                      busy={handOver.isPending}
                      onPress={() =>
                        handOver.mutate(note.code, {
                          onSuccess: () => toast.show(t('bl.handoverDone')),
                          onError: (error: Error) =>
                            toast.show(describeActionError(error, t), 'error'),
                        })
                      }
                    />
                  ) : null}
                </Card>
              </FadeIn>
            ))}
          </>
        ) : null}
      </ScrollView>
    </Screen>
  );
}
