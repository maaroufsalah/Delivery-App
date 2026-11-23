import { findParcelStatus, routeTargetsSender, splitCheque } from '@delivery/shared';
import * as Haptics from 'expo-haptics';
import { FileSignature, PackageCheck, Square, SquareCheck } from 'lucide-react-native';
import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { ActionFeedbackLine } from '@/components/action-feedback';
import { useI18n } from '@/i18n';
import { isDeferred, useOutcomes, useParcelBl, useParcelDetail, useReport } from '@/lib/courier';
import { lastKnownCoords } from '@/lib/geo';
import { scanFeedback } from '@/lib/scan-feedback';
import { useAction } from '@/lib/use-action';
import { Button, Field, Sheet, useToast } from '@/ui';

/** Les deux issues que le panneau nomme (DOMAIN.md §2) : Livré, et Reporté (date). */
const DELIVERED = 4;
const POSTPONED = 14;

/**
 * LIVRER EN UN SEUL APPUI — chantier « Livrer en un appui » (D-156, audit
 * MOB-04).
 *
 * Après un scan, la carte disait « Livrer » mais ouvrait la fiche, où il
 * fallait appuyer encore ; l'icône « Rapporter » du circuit faisait pareil.
 * Deux écrans pour une action. Ce panneau EST l'action, et il est le même aux
 * trois endroits — la fiche du colis, la carte du scan, la ligne du circuit :
 *
 *  - « Livrer · X MAD » livre tout de suite, avec le chèque facultatif
 *    (numéro et banque dès qu'un montant est saisi), les espèces calculées en
 *    direct (`splitCheque`), les mêmes refus que la fiche et que l'API ;
 *  - « Autre statut » ouvre la feuille des SEULES issues que le serveur
 *    autorise (`GET /outcomes`), avec commentaire et date de report ;
 *  - rien n'est nouveau côté serveur : même `POST /courier/parcels/:code/status`,
 *    même file hors ligne, même `Idempotency-Key` (`useReport`), même garde
 *    d'appui unique (`useAction`).
 *
 * La fiche reste la consultation ; elle rend ce panneau comme les autres.
 */
export function DeliverPanel({
  code,
  codToCollect,
  kind,
  statusId,
  initialPicker = false,
  onDone,
}: {
  code: string;
  codToCollect: number;
  kind: string;
  /**
   * Le statut que l'écran connaît déjà — lot C, point C3. Les issues permises
   * s'en déduisent tout de suite (`courierOutcomesFor`, la règle du serveur) :
   * le panneau s'ouvre complet, au lieu de grandir quand l'API répond.
   */
  statusId?: number;
  /** Ouvrir d'emblée la feuille « Autre statut » (entrée par `?report=1` / `?other=1`). */
  initialPicker?: boolean;
  /** Après l'issue rendue : revenir, fermer, repasser en « prêt à scanner ». */
  onDone: (statusId: number) => void;
}) {
  const { t, status } = useI18n();
  const toast = useToast();
  const outcomesQuery = useOutcomes(code, statusId, kind);
  const detail = useParcelDetail(code);
  const report = useReport(code);
  const deliverAction = useAction();
  const otherAction = useAction();

  const [picker, setPicker] = useState(initialPicker);
  const [chosen, setChosen] = useState<number | null>(null);
  const [chequeAmount, setChequeAmount] = useState('');
  const [chequeNumber, setChequeNumber] = useState('');
  const [chequeBank, setChequeBank] = useState('');
  const [comment, setComment] = useState('');
  const [scheduledAt, setScheduledAt] = useState('');

  /*
   * « BL RÉCUPÉRÉ » — lot E, E1.1 (D-201). Sur un colis de Client Pro qui
   * demande son BL, et tant qu'aucun BL n'est enregistré, la case part AVEC
   * « Livrer » : même requête, même file hors ligne, toujours un seul appui
   * pour livrer. Décochée par défaut — un BL coché par erreur serait un papier
   * que l'agence chercherait.
   */
  const blWanted = detail.data?.blRequested === true;
  const blTracking = useParcelBl(blWanted ? code : undefined);
  const blPending = blWanted && blTracking.data?.slip === null;
  const [blCollected, setBlCollected] = useState(false);
  const [blReference, setBlReference] = useState('');

  const outcomes = outcomesQuery.data ?? [];
  const toSender = routeTargetsSender(kind);
  const canDeliver = outcomes.includes(DELIVERED);
  const otherOutcomes = outcomes.filter((statusId) => statusId !== DELIVERED);
  const cheque = splitCheque(codToCollect, chequeAmount);
  const picked = chosen === null ? undefined : findParcelStatus(chosen);
  const expectedPayment = detail.data?.expectedPayment ?? null;

  /** Une issue, envoyée par le bouton qui la porte — appui unique, délai, retour garanti. */
  const send = (statusId: number, action: ReturnType<typeof useAction>) => {
    const delivered = statusId === DELIVERED;

    void action.run(
      async () => {
        const coords = await lastKnownCoords();
        return report.mutateAsync({
          statusId,
          comment: delivered ? undefined : comment.trim() || undefined,
          collectedAmount: delivered ? codToCollect / 100 : undefined,
          chequeAmount: delivered && cheque.chequeCents > 0 ? cheque.chequeCents / 100 : undefined,
          chequeNumber:
            delivered && cheque.chequeCents > 0 ? chequeNumber.trim() || undefined : undefined,
          chequeBank:
            delivered && cheque.chequeCents > 0 ? chequeBank.trim() || undefined : undefined,
          scheduledAt: !delivered && chosen === POSTPONED && scheduledAt ? scheduledAt : undefined,
          ...(delivered && blPending && blCollected
            ? { blCollected: true, blReference: blReference.trim() || undefined }
            : {}),
          ...coords,
        });
      },
      {
        onSuccess: (result) => {
          // Confirmation SONORE et visuelle : le bip du scanner, le toast, la vibration.
          scanFeedback(isDeferred(result) ? 'queued' : 'accepted');
          void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
          setPicker(false);
          const label = findParcelStatus(statusId);
          toast.show(
            isDeferred(result)
              ? t('scan.queued')
              : delivered
                ? t('parcelAction.delivered', { code })
                : t('parcelAction.reported', {
                    code,
                    status: label ? status(label.key) : String(statusId),
                  }),
            isDeferred(result) ? 'info' : 'success',
          );
          onDone(statusId);
        },
      },
    );
  };

  return (
    <View className="gap-3">
      {canDeliver ? (
        <>
          {expectedPayment ? (
            <Text className="text-[14px] text-muted-foreground">
              {`${t('parcel.expectedPayment')} : ${
                expectedPayment === 'cheque'
                  ? t('expectedPayment.cheque')
                  : t('expectedPayment.cash')
              }`}
            </Text>
          ) : null}

          {toSender ? null : (
            <View className="gap-2">
              <Field
                label={t('parcelAction.chequeOptional')}
                value={chequeAmount}
                onChangeText={setChequeAmount}
                placeholder="0.00"
                inputMode="decimal"
              />
              <Text
                className={
                  cheque.tooLarge || cheque.invalid
                    ? 'text-[14px] text-destructive'
                    : 'text-[15px] font-semibold text-foreground'
                }
              >
                {cheque.invalid
                  ? t('parcelAction.chequeInvalid')
                  : cheque.tooLarge
                    ? t('parcel.chequeTooLarge')
                    : `${t('parcel.cashComputed')} ${(cheque.cashCents / 100).toFixed(2)} MAD`}
              </Text>

              {cheque.chequeCents > 0 ? (
                <>
                  <Field
                    label={t('parcel.chequeNumber')}
                    value={chequeNumber}
                    onChangeText={setChequeNumber}
                    autoCapitalize="none"
                  />
                  <Field
                    label={t('parcel.chequeBank')}
                    value={chequeBank}
                    onChangeText={setChequeBank}
                  />
                </>
              ) : null}
            </View>
          )}

          {blPending ? (
            <View className="gap-2">
              <Pressable
                accessibilityRole="checkbox"
                accessibilityState={{ checked: blCollected }}
                onPress={() => setBlCollected((value) => !value)}
                hitSlop={4}
                className={`min-h-[52px] flex-row items-center gap-3 rounded-2xl border p-3 ${
                  blCollected ? 'border-2 border-primary bg-muted' : 'border-border'
                }`}
                style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
              >
                {blCollected ? (
                  <SquareCheck size={22} className="text-primary" />
                ) : (
                  <Square size={22} className="text-muted-foreground" />
                )}
                <FileSignature size={18} className="text-primary" />
                <Text className="flex-1 text-[16px] text-foreground">{t('bl.deliverToggle')}</Text>
              </Pressable>
              {blCollected ? (
                <Field
                  label={t('bl.reference')}
                  value={blReference}
                  onChangeText={setBlReference}
                  autoCapitalize="characters"
                />
              ) : null}
            </View>
          ) : null}

          <Button
            label={
              toSender
                ? t('parcelAction.deliver')
                : t('parcelAction.deliverAmount', { amount: (codToCollect / 100).toFixed(2) })
            }
            icon={PackageCheck}
            onPress={() => send(DELIVERED, deliverAction)}
            disabled={!cheque.canDeliver || otherAction.pending}
            busy={deliverAction.pending}
            full
          />
          <ActionFeedbackLine feedback={deliverAction.failure} />
        </>
      ) : null}

      {otherOutcomes.length > 0 ? (
        <Button
          // Sans « Livré » (circuit de retour, D-179), c'est l'action du panneau.
          label={canDeliver ? t('parcelAction.otherStatus') : t('parcel.report')}
          tone={canDeliver ? 'ghost' : 'primary'}
          onPress={() => {
            otherAction.clearError();
            setPicker(true);
          }}
          disabled={deliverAction.pending}
          full
        />
      ) : null}

      {/*
        « AUTRE STATUT » : la liste des SEULES issues que le serveur autorise,
        en feuille — on choisit, on commente si besoin, on confirme. L'erreur
        s'affiche DANS la feuille (cause du blocage du 03/10, Recette 5).
      */}
      <Sheet
        open={picker}
        onClose={() => {
          if (!otherAction.pending) setPicker(false);
        }}
        title={t('parcelAction.otherStatus')}
      >
        <View className="gap-2 pb-2">
          {otherOutcomes.map((statusId) => {
            const option = findParcelStatus(statusId);
            if (!option) return null;

            return (
              <Pressable
                key={statusId}
                accessibilityRole="radio"
                accessibilityState={{ selected: chosen === statusId }}
                onPress={() => setChosen(statusId)}
                hitSlop={4}
                className={`min-h-[52px] flex-row items-center gap-3 rounded-2xl border p-3 ${
                  chosen === statusId ? 'border-2 border-primary bg-muted' : 'border-border'
                }`}
                style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
              >
                <View className="size-3 rounded-full" style={{ backgroundColor: option.color }} />
                <Text className="text-[16px] text-foreground">{status(option.key)}</Text>
              </Pressable>
            );
          })}

          {picked ? (
            <View className="gap-2 pt-2">
              {chosen === POSTPONED ? (
                <Field
                  label={t('parcel.scheduledAt')}
                  value={scheduledAt}
                  onChangeText={setScheduledAt}
                  placeholder="2026-10-05"
                  autoCapitalize="none"
                />
              ) : null}
              <Field
                label={t('parcel.reason')}
                value={comment}
                onChangeText={setComment}
                multiline
              />
              <Button
                label={t('parcelAction.confirmStatus', { status: status(picked.key) })}
                onPress={() => {
                  if (chosen !== null) send(chosen, otherAction);
                }}
                busy={otherAction.pending}
                full
              />
              <ActionFeedbackLine feedback={otherAction.failure} />
            </View>
          ) : null}
        </View>
      </Sheet>
    </View>
  );
}
