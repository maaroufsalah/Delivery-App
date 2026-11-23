import { PASSWORD_MIN_LENGTH } from '@delivery/shared';
import { KeyRound } from 'lucide-react-native';
import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { changeOwnPassword } from '@/lib/account';
import { useSession } from '@/lib/session';
import { Button, Card, Field } from '@/components/ui';

/**
 * CHANGER SON MOT DE PASSE, depuis le profil — C12 bloc 2c.
 *
 * **Dans le profil partagé, donc pour les trois espaces d'un coup.** Livreur,
 * e-commerçant et staff changent leur mot de passe de la même façon ; en
 * recopier trois versions garantirait qu'une seule reçoive le prochain
 * correctif.
 *
 * **Replié tant qu'on n'appuie pas.** Trois champs de saisie ouverts en
 * permanence au milieu des réglages donneraient à croire qu'il y a quelque
 * chose à remplir à chaque visite.
 *
 * **La déconnexion suit le succès, et ce n'est pas une punition.** Le serveur
 * révoque toutes les sessions au changement — c'est ce qui rend le geste utile
 * quand le mot de passe avait fuité. Le jeton du téléphone tombe avec les
 * autres : le garder ferait continuer l'application avec un jeton mort, qui
 * échouerait au premier écran suivant sans rien expliquer.
 */
export function PasswordCard() {
  const { t } = useI18n();
  const { signOut } = useSession();

  const [open, setOpen] = useState(false);
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const ready =
    current.length > 0 && next.length >= PASSWORD_MIN_LENGTH && confirmation.length > 0 && !busy;

  const submit = async () => {
    // Les deux refus qu'on peut prononcer sans le réseau. Le serveur revérifie
    // tout, mais lui demander de dire « les deux ne correspondent pas » est un
    // aller-retour pour une évidence.
    if (next !== confirmation) {
      setError(t('password.mismatch'));
      return;
    }

    if (next === current) {
      setError(t('password.unchanged'));
      return;
    }

    setBusy(true);
    setError(null);

    try {
      await changeOwnPassword({ currentPassword: current, password: next, confirmation });
      await signOut();
    } catch {
      // Un seul message : le refus le plus fréquent, et de loin, est l'ancien
      // mot de passe mal tapé.
      setError(t('password.failed'));
      setBusy(false);
    }
  };

  if (!open) {
    return (
      <Pressable accessibilityRole="button" onPress={() => setOpen(true)}>
        <Card className="flex-row items-center gap-3">
          <KeyRound size={20} className="text-muted-foreground" />
          <Text className="flex-1 text-[16px] text-foreground">{t('password.title')}</Text>
        </Card>
      </Pressable>
    );
  }

  return (
    <Card className="gap-3">
      <View className="flex-row items-center gap-2">
        <KeyRound size={20} className="text-muted-foreground" />
        <Text className="flex-1 text-[16px] font-medium text-foreground">
          {t('password.title')}
        </Text>
      </View>

      <Text className="text-[13px] text-muted-foreground">
        {t('password.hint', { min: PASSWORD_MIN_LENGTH })}
      </Text>

      <Field
        label={t('password.current')}
        value={current}
        onChangeText={setCurrent}
        secureTextEntry
        autoCapitalize="none"
        autoComplete="current-password"
      />
      <Field
        label={t('password.new')}
        value={next}
        onChangeText={setNext}
        secureTextEntry
        autoCapitalize="none"
        autoComplete="new-password"
      />
      <Field
        label={t('password.confirmation')}
        value={confirmation}
        onChangeText={setConfirmation}
        secureTextEntry
        autoCapitalize="none"
        autoComplete="new-password"
        onSubmitEditing={() => void submit()}
      />

      {error ? <Text className="text-[15px] text-destructive">{error}</Text> : null}

      <Text className="text-[13px] text-muted-foreground">{t('password.willSignOut')}</Text>

      <View className="flex-row gap-2">
        <View className="flex-1">
          <Button
            label={t('common.cancel')}
            tone="ghost"
            onPress={() => {
              setOpen(false);
              setCurrent('');
              setNext('');
              setConfirmation('');
              setError(null);
            }}
            full
          />
        </View>
        <View className="flex-1">
          <Button
            label={t('password.submit')}
            onPress={() => void submit()}
            busy={busy}
            disabled={!ready}
            full
          />
        </View>
      </View>
    </Card>
  );
}
