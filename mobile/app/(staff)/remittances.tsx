import { useRouter } from 'expo-router';
import { ArrowLeft, Banknote, Check } from 'lucide-react-native';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, Text, View } from 'react-native';
import type { RemittanceStage } from '@delivery/shared';
import { useI18n } from '@/i18n';
import { describeActionError } from '@/lib/use-action';
import {
  REMITTANCE_STAGE_LABEL,
  remittanceStageOf,
  remittanceStageTone,
} from '@/lib/remittance-close';
import { can, useSession } from '@/lib/session';
import { useConfirmRemittance, useCountRemittance, useRemittances } from '@/lib/staff';
import { useTokens } from '@/theme/tenant-theme';
import { Button, Card, EmptyState, Field, Screen, Skeleton, StatusBadge } from '@/ui';

/**
 * REMISES DE FONDS — M3 bloc 8.
 *
 * Le livreur rapporte le cash, l'agence le COMPTE, puis l'agence CONFIRME.
 * Deux gestes, deux permissions — `finance.write` pour compter, `finance.confirm`
 * pour attester — et la matrice peut les séparer. Chaque bouton est donc gardé
 * par sa propre clé.
 *
 * **La mission disait `finance.pay`.** C'est `finance.confirm` qui garde la
 * confirmation d'une remise : `finance.pay` règle un relevé ou un bon de
 * versement, ce qui est un autre geste. L'esprit est respecté — un agent sans
 * la clé ne voit pas le bouton — et c'est la clé RÉELLE qui est lue, sinon
 * l'écran mentirait dans les deux sens.
 *
 * **L'écart demande un motif, et la règle vient du schéma partagé** : le montant
 * attendu repart avec le montant compté pour que la vérification se fasse au
 * même endroit des deux côtés. Sans motif, c'est le serveur qui refuse — et cet
 * écran le dit avant d'essayer.
 *
 * **Chaque carte dit où en est la caisse** (D-243) : « ouverte » tant que le
 * livreur y verse ses livraisons, « fermée » quand il l'a clôturée et que
 * l'argent attend d'être réceptionné. Le statut vient du serveur (`stage`),
 * l'écran n'en dérive aucun, et aucun geste ne s'ajoute ici.
 */
export default function StaffRemittancesScreen() {
  const { t } = useI18n();
  const router = useRouter();
  const { profile } = useSession();

  const pending = useRemittances('draft');

  const mayCount = can(profile, 'finance.write');
  const mayConfirm = can(profile, 'finance.confirm');

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
          {t('staff.pendingRemittances')}
        </Text>
      </View>

      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        className="flex-1"
      >
        {pending.isLoading ? (
          <View className="gap-2 px-5">
            <Skeleton className="h-32" />
            <Skeleton className="h-32" />
          </View>
        ) : (pending.data?.items ?? []).length === 0 ? (
          <EmptyState icon={Banknote} title={t('staff.noRemittances')} />
        ) : (
          <ScrollView
            keyboardShouldPersistTaps="handled"
            contentContainerClassName="gap-3 px-5 pb-6"
          >
            {(pending.data?.items ?? []).map((row) => (
              <RemittanceCard
                key={row.id}
                code={row.code}
                stage={remittanceStageOf(row)}
                courierName={row.courierName}
                cityName={row.cityName}
                expectedNet={row.expectedNet}
                mayCount={mayCount}
                mayConfirm={mayConfirm}
              />
            ))}
          </ScrollView>
        )}
      </KeyboardAvoidingView>
    </Screen>
  );
}

function RemittanceCard({
  code,
  stage,
  courierName,
  cityName,
  expectedNet,
  mayCount,
  mayConfirm,
}: {
  code: string;
  stage: RemittanceStage;
  courierName: string;
  cityName: string;
  expectedNet: number;
  mayCount: boolean;
  mayConfirm: boolean;
}) {
  const { t } = useI18n();
  const tokens = useTokens();

  const count = useCountRemittance(code);
  const confirm = useConfirmRemittance(code);

  const [declared, setDeclared] = useState('');
  const [reason, setReason] = useState('');
  const [note, setNote] = useState<string | null>(null);

  /** En CENTIMES : aucun flottant n'entre dans un écart de caisse. */
  const declaredCents = Math.round((Number(declared.replace(',', '.')) || 0) * 100);
  const variance = declared === '' ? 0 : declaredCents - expectedNet;
  const needsReason = variance !== 0 && reason.trim() === '';

  const save = () => {
    count.mutate(
      {
        // Le schéma partagé attend des DIRHAMS et convertit ; on lui rend ce
        // que l'agent a tapé, pas une conversion de plus.
        declaredAmount: Number(declared.replace(',', '.')) || 0,
        expectedNet: expectedNet / 100,
        varianceReason: reason.trim() || undefined,
      },
      {
        onSuccess: () => setNote(null),
        onError: (error) => setNote(describeActionError(error, t)),
      },
    );
  };

  return (
    <Card className="gap-3">
      <View className="flex-row items-start justify-between gap-3">
        <View className="flex-1">
          <Text className="font-mono text-[15px] text-muted-foreground">{code}</Text>
          <Text className="text-[16px] font-semibold text-foreground">{courierName}</Text>
          <Text className="text-[14px] text-muted-foreground">{cityName}</Text>
        </View>

        {/* La couleur est un jeton du thème, lu en valeur : React Native ne
            résout pas les variables CSS. */}
        <StatusBadge
          label={t(REMITTANCE_STAGE_LABEL[stage])}
          color={tokens[remittanceStageTone(stage)]}
        />
      </View>

      <View className="gap-1 border-t border-border pt-2">
        <Row label={t('staff.expected')} value={money(expectedNet)} />
        {declared !== '' ? (
          <Row
            label={t('staff.variance')}
            value={`${variance > 0 ? '+' : ''}${money(variance)}`}
            tone={variance === 0 ? undefined : 'warning'}
          />
        ) : null}
      </View>

      {mayCount ? (
        <>
          <Field
            label={t('staff.declared')}
            value={declared}
            onChangeText={setDeclared}
            inputMode="decimal"
          />

          {/* Le motif n'apparaît que s'il y a un écart : le demander sur un
              compte juste ferait chercher une explication qui n'existe pas. */}
          {variance !== 0 && declared !== '' ? (
            <>
              <Field label={t('staff.varianceReason')} value={reason} onChangeText={setReason} />
              {needsReason ? (
                <Text className="text-[14px] text-warning">{t('staff.varianceNeedsReason')}</Text>
              ) : null}
            </>
          ) : null}

          <Button
            label={t('staff.count')}
            onPress={save}
            disabled={declared === '' || needsReason}
            busy={count.isPending}
            full
          />
        </>
      ) : null}

      {mayConfirm ? (
        <Button
          label={t('staff.confirm')}
          icon={Check}
          tone="ghost"
          onPress={() =>
            confirm.mutate(undefined, {
              onError: (error) => setNote(describeActionError(error, t)),
            })
          }
          busy={confirm.isPending}
          full
        />
      ) : null}

      {note ? <Text className="text-[14px] text-destructive">{note}</Text> : null}
    </Card>
  );
}

function Row({ label, value, tone }: { label: string; value: string; tone?: 'warning' }) {
  return (
    <View className="flex-row items-baseline justify-between gap-3">
      <Text className="text-[15px] text-muted-foreground">{label}</Text>
      <Text
        className={`text-[16px] font-semibold tabular-nums ${
          tone === 'warning' ? 'text-warning' : 'text-foreground'
        }`}
      >
        {value}
      </Text>
    </View>
  );
}

function money(cents: number): string {
  return `${(cents / 100).toFixed(2)} MAD`;
}
