import { AlertTriangle, CloudOff, RefreshCw } from 'lucide-react-native';
import { useCallback, useEffect, useState } from 'react';
import { AppState, Modal, Pressable, ScrollView, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useQueryClient } from '@tanstack/react-query';
import { useI18n } from '@/i18n';
import { useSession } from '@/lib/session';
import { formatStamp } from '@/lib/datetime';
import { Button } from '@/components/ui';
import {
  clearRejected,
  drain,
  pending,
  rejected,
  type QueuedItem,
  type RejectedItem,
} from '@/lib/queue';

/**
 * L'état de la FILE, visible de partout — M1, docs/DECISIONS.md D-077.
 *
 * **Un geste en attente doit se voir.** Un livreur qui a dit « livré » dans une
 * cage d'escalier doit savoir que l'information n'est pas encore partie : sans
 * ce bandeau, il croirait son travail enregistré et découvrirait le soir qu'il
 * ne l'était pas.
 *
 * **Le drain part au retour de l'application au premier plan**, et non sur une
 * minuterie : c'est le moment où le téléphone retrouve du réseau — il sort de
 * la poche, de l'ascenseur, du parking. Une minuterie réveillerait la radio
 * toutes les trente secondes pour rien.
 *
 * Le bandeau se touche : un livreur impatient peut forcer l'envoi. C'est le
 * seul endroit où il a prise sur la file, et ça vaut mieux que de le laisser
 * regarder un compteur qui ne bouge pas.
 *
 * **Les gestes REFUSÉS ont leur propre bandeau, rouge, et il ne disparaît pas
 * tout seul.** Un refus métier sort de la file (D-077) : sans cet écran, le
 * geste serait perdu en silence, et le livreur croirait son colis livré alors
 * que le serveur a dit non. Il faut le toucher, lire la raison, et l'acquitter
 * — c'est le seul endroit de l'application où on demande un accusé de lecture.
 */
export function OfflineBanner() {
  const { t, locale } = useI18n();
  const client = useQueryClient();
  const [items, setItems] = useState<QueuedItem[]>([]);
  const [refused, setRefused] = useState<RejectedItem[]>([]);
  const [busy, setBusy] = useState(false);
  const [open, setOpen] = useState(false);
  const { degraded } = useSession();

  const refresh = useCallback(async () => {
    const [queued, denied] = await Promise.all([pending(), rejected()]);
    setItems(queued);
    setRefused(denied);
  }, []);

  const acknowledge = useCallback(async () => {
    await clearRejected();
    setOpen(false);
    await refresh();
  }, [refresh]);

  const flush = useCallback(async () => {
    setBusy(true);

    try {
      const result = await drain();

      if (result.sent > 0) {
        /*
         * Ce qui est parti a changé les listes — la tournée d'un livreur, les
         * colis d'un vendeur. On invalide les DEUX racines : le bandeau est
         * monté dans les deux espaces, et n'a pas à savoir dans lequel il est.
         */
        void client.invalidateQueries({ queryKey: ['courier'] });
        void client.invalidateQueries({ queryKey: ['merchant'] });
      }
    } finally {
      setBusy(false);
      await refresh();
    }
  }, [client, refresh]);

  useEffect(() => {
    /*
     * `refresh` LIT un magasin externe — la file persistée — et pose le
     * résultat. La règle voit `setItems` sous un effet et proteste ; elle ne
     * voit pas que la pose arrive APRÈS un `await`, donc jamais pendant le
     * rendu. Un compteur qui n'apparaît qu'au bout de cinq secondes, lui,
     * laisserait croire à un livreur que son geste n'est pas parti.
     */
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void refresh();

    const subscription = AppState.addEventListener('change', (state) => {
      if (state === 'active') void flush();
    });

    /*
     * Un rafraîchissement lent du COMPTEUR, pas du drain : les écrans mettent
     * des entrées en file sans prévenir ce composant, et un compteur figé à
     * zéro pendant que la file grossit serait un mensonge.
     */
    const timer = setInterval(() => void refresh(), 5_000);

    return () => {
      subscription.remove();
      clearInterval(timer);
    };
  }, [flush, refresh]);

  if (items.length === 0 && refused.length === 0 && !degraded) return null;

  return (
    <View>
      {/*
        « Hors ligne, nouvel essai… » — Recette 7, point 7 (MOB-01) : `/auth/me`
        n'a pas répondu au lancement, la session est gardée sur le dernier
        profil connu et le client retente ; le livreur le sait, sans alarme.
      */}
      {degraded ? (
        <View className="flex-row items-center justify-center gap-2 bg-muted px-4 py-1.5">
          <CloudOff size={14} className="text-muted-foreground" />
          <Text className="text-[13px] text-muted-foreground">{t('queue.reconnecting')}</Text>
        </View>
      ) : null}
      {items.length > 0 ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('queue.pending', { count: items.length })}
          onPress={() => void flush()}
          // Pas de second rejeu pendant le premier (audit du 03/10, C2).
          disabled={busy}
          // Le jaune d'avertissement du référentiel, pas une couleur inventée.
          className="flex-row items-center justify-center gap-2 bg-warning px-4 py-2"
        >
          {busy ? (
            <RefreshCw size={16} className="text-foreground" />
          ) : (
            <CloudOff size={16} className="text-foreground" />
          )}
          <Text className="text-[14px] font-medium text-foreground">
            {t('queue.pending', { count: items.length })}
          </Text>
        </Pressable>
      ) : null}

      {refused.length > 0 ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('queue.rejected', { count: refused.length })}
          onPress={() => setOpen(true)}
          className="flex-row items-center justify-center gap-2 bg-destructive px-4 py-2"
        >
          <AlertTriangle size={16} className="text-destructive-foreground" />
          <Text className="text-[14px] font-medium text-destructive-foreground">
            {t('queue.rejected', { count: refused.length })}
          </Text>
        </Pressable>
      ) : null}

      <Modal visible={open} animationType="slide" onRequestClose={() => setOpen(false)}>
        <SafeAreaView className="flex-1 bg-background" edges={['top', 'bottom']}>
          <ScrollView
            keyboardShouldPersistTaps="handled"
            contentContainerClassName="gap-3 px-5 pb-6 pt-4"
          >
            <Text className="text-[22px] font-bold text-foreground">
              {t('queue.rejectedTitle')}
            </Text>
            <Text className="text-[15px] text-muted-foreground">{t('queue.rejectedHint')}</Text>

            {refused.map((row) => (
              <View key={row.id} className="gap-1 rounded-xl border border-border p-3">
                <Text className="text-[16px] font-semibold text-foreground">{row.label}</Text>
                {/*
                  Le message du serveur, tel quel. Le traduire demanderait de
                  connaître ici tous les refus de l'API ; le livreur, lui, a
                  besoin de pouvoir le lire à l'agence, pas de l'interpréter.
                */}
                <Text className="text-[15px] text-foreground">{row.message}</Text>
                <Text className="text-[13px] text-muted-foreground">
                  {formatStamp(row.at, locale)}
                </Text>
              </View>
            ))}
          </ScrollView>

          <View className="px-5 pb-4">
            <Button label={t('queue.rejectedClear')} onPress={() => void acknowledge()} full />
          </View>
        </SafeAreaView>
      </Modal>
    </View>
  );
}
