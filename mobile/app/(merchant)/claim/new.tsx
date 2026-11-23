import * as ImagePicker from 'expo-image-picker';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useFreshKey } from '@/lib/use-fresh-key';
import { ArrowLeft, Camera, Check, Send } from 'lucide-react-native';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, Text, View } from 'react-native';
import { useGoBack } from '@/lib/go-back';
import { useI18n } from '@/i18n';
import { describeActionError } from '@/lib/use-action';
import { useClaimTypes, useOpenClaim } from '@/lib/claims';
import { Button, Chip, Field, Screen } from '@/ui';

/**
 * OUVRIR UNE RÉCLAMATION — M2 bloc 6.
 *
 * Atteinte depuis « Signaler un problème » sur un colis, qui pré-remplit son
 * code : c'est le chemin réel, et demander à un vendeur de recopier
 * `CAS-RAB-000000123` depuis l'écran précédent serait une invitation à se
 * tromper.
 *
 * **La photo passe par la GALERIE, pas par l'appareil.** Un problème se
 * constate après coup — un colis rendu abîmé, une capture d'écran d'un client
 * mécontent — et la photo existe déjà. `expo-camera` reste pour le livreur, qui
 * photographie ce qu'il a sous les yeux.
 *
 * Les métadonnées EXIF sont retirées à la sélection : une photo prise chez soi
 * porte les coordonnées de son domicile, et rien ici n'en a besoin.
 */
function NewClaimScreenContent() {
  const { t } = useI18n();
  const router = useRouter();
  // Une notification ouvre cette fiche sans rien derrière : on se rabat.
  const goBack = useGoBack('/(merchant)/claims');
  const { parcelCode } = useLocalSearchParams<{ parcelCode?: string }>();

  const types = useClaimTypes();
  const open = useOpenClaim();

  const [typeId, setTypeId] = useState('');
  const [subject, setSubject] = useState('');
  const [message, setMessage] = useState('');
  const [photo, setPhoto] = useState<{
    filename: string;
    mimeType: string;
    content: string;
  } | null>(null);
  const [note, setNote] = useState<string | null>(null);

  const pick = async () => {
    const result = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ['images'],
      quality: 0.5,
      base64: true,
      // Les coordonnées de prise de vue n'ont rien à faire sur une réclamation.
      exif: false,
    });

    const asset = result.assets?.[0];
    if (asset?.base64) {
      setPhoto({ filename: 'reclamation.jpg', mimeType: 'image/jpeg', content: asset.base64 });
    }
  };

  const submit = () => {
    open.mutate(
      {
        typeId,
        subject: subject.trim(),
        message: message.trim(),
        parcelCode: parcelCode || undefined,
        attachments: photo ? [photo] : undefined,
      },
      {
        onSuccess: (claim) => router.replace(`/(merchant)/claim/${encodeURIComponent(claim.code)}`),
        onError: (error) => setNote(describeActionError(error, t)),
      },
    );
  };

  /*
   * Les MÊMES bornes que `createClaimSchema` : cinq caractères pour l'objet,
   * dix pour le message. Plus permissif ici, le bouton aurait laissé partir
   * une requête que le serveur refuse — après que le vendeur a tout écrit.
   * Plus strict, il aurait refusé ce que le serveur accepte.
   *
   * Et le colis est exigé quand le motif le réclame : la règle vit au service,
   * qui seul connaît le type, mais la redire ici évite un aller-retour pour
   * une information qu'on a déjà.
   */
  const type = (types.data ?? []).find((row) => row.id === typeId);

  const ready =
    typeId !== '' &&
    subject.trim().length > 0 &&
    message.trim().length > 0 &&
    (type?.subjectType !== 'parcel' || Boolean(parcelCode));

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
        <Text className="flex-1 text-[20px] font-bold text-foreground">{t('claims.new')}</Text>
      </View>

      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        className="flex-1"
      >
        <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-4 px-5 pb-6">
          {parcelCode ? (
            <Text className="font-mono text-[15px] text-muted-foreground">{parcelCode}</Text>
          ) : null}

          <View className="gap-2">
            <Text className="text-[14px] font-medium text-muted-foreground">
              {t('claims.type')}
            </Text>
            <ScrollView
              keyboardShouldPersistTaps="handled"
              horizontal
              showsHorizontalScrollIndicator={false}
              contentContainerClassName="gap-2"
            >
              {(types.data ?? []).map((type) => (
                <Chip
                  key={type.id}
                  label={type.label}
                  active={typeId === type.id}
                  onPress={() => setTypeId(type.id)}
                />
              ))}
            </ScrollView>
          </View>

          <Field label={t('claims.subject')} value={subject} onChangeText={setSubject} />
          <Field label={t('claims.message')} value={message} onChangeText={setMessage} multiline />

          <Button
            label={photo ? t('claims.photoAdded') : t('claims.addPhoto')}
            icon={photo ? Check : Camera}
            tone="ghost"
            onPress={() => void pick()}
            full
          />

          {note ? <Text className="text-[15px] text-destructive">{note}</Text> : null}
        </ScrollView>

        <View className="px-5 pb-4">
          <Button
            label={t('claims.send')}
            icon={Send}
            onPress={submit}
            disabled={!ready}
            busy={open.isPending}
            full
          />
        </View>
      </KeyboardAvoidingView>
    </Screen>
  );
}

/** Coque Recette 7 (point 5) : le contenu repart vide à chaque `parcelCode` et à chaque fermeture — `useFreshKey`. */
export default function NewClaimScreen() {
  const { parcelCode } = useLocalSearchParams<{ parcelCode?: string }>();
  return <NewClaimScreenContent key={useFreshKey(parcelCode)} />;
}
