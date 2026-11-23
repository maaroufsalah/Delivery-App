import * as ImagePicker from 'expo-image-picker';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useFreshKey } from '@/lib/use-fresh-key';
import { Camera, Check, Send } from 'lucide-react-native';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { describeActionError } from '@/lib/use-action';
import { useClaimTypes, useOpenClaim } from '@/lib/claims';
import { Button, Chip, Field, Screen, ScreenHeader } from '@/ui';

/**
 * OUVRIR UNE RÉCLAMATION, CÔTÉ LIVREUR — le complément de L2.
 *
 * **Il ne choisit pas l'e-commerçant, et l'écran ne le lui demande pas.** Il
 * réclame À PROPOS D'UN COLIS — « arrivé abîmé », « adresse fausse »,
 * « destinataire introuvable depuis trois jours » — et le serveur déduit
 * l'e-commerçant du colis. Le lui faire choisir dans une liste reviendrait à
 * lui montrer l'annuaire des clients de la société, ce que D-082 interdit.
 *
 * **Le colis est EXIGÉ, quel que soit le motif.** C'est la différence avec le
 * formulaire du marchand, où le type décide : `resolveClient()` refuse un
 * livreur sans code de colis, puisque c'est le colis qui désigne le client. Le
 * champ n'est donc jamais masqué, et le bouton reste éteint tant qu'il est
 * vide — plutôt que de laisser partir une requête que le serveur refusera
 * après que tout a été écrit.
 *
 * **La photo passe par la GALERIE**, comme chez le marchand. Le geste du
 * livreur qui photographie ce qu'il a sous les yeux existe déjà ailleurs — la
 * preuve de livraison, le chèque — et passe par `expo-camera`. Ici la
 * réclamation s'ouvre le plus souvent après coup, de retour à l'agence, sur
 * une photo déjà prise. Les métadonnées EXIF sont retirées à la sélection.
 */
function NewCourierClaimScreenContent() {
  const { t } = useI18n();
  const router = useRouter();
  const { parcelCode: preset } = useLocalSearchParams<{ parcelCode?: string }>();

  const types = useClaimTypes();
  const open = useOpenClaim();

  const [typeId, setTypeId] = useState('');
  const [parcelCode, setParcelCode] = useState(preset ?? '');
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
      exif: false,
    });

    const asset = result.assets?.[0];
    if (asset?.base64) {
      setPhoto({ filename: 'reclamation.jpg', mimeType: 'image/jpeg', content: asset.base64 });
    }
  };

  /*
   * Les MÊMES bornes que `createClaimSchema` : cinq caractères pour l'objet,
   * dix pour le message — plus le colis, que le service exige d'un livreur.
   */
  const ready =
    typeId !== '' &&
    parcelCode.trim() !== '' &&
    subject.trim().length > 0 &&
    message.trim().length > 0;

  const submit = () => {
    open.mutate(
      {
        typeId,
        subject: subject.trim(),
        message: message.trim(),
        parcelCode: parcelCode.trim(),
        attachments: photo ? [photo] : undefined,
      },
      {
        onSuccess: (claim) => router.replace(`/(courier)/claim/${encodeURIComponent(claim.code)}`),
        /*
         * Le message du serveur TEL QUEL : « Ce colis ne vous est pas
         * confié : CAS-… » dit exactement quoi corriger, là où un texte
         * générique obligerait à deviner.
         */
        onError: (error) => setNote(describeActionError(error, t)),
      },
    );
  };

  return (
    <Screen>
      <ScreenHeader title={t('claims.new')} backHref={'/(courier)/claims'} />

      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        className="flex-1"
      >
        <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-4 px-5 pb-6">
          <Field
            label={t('claims.parcel')}
            value={parcelCode}
            onChangeText={setParcelCode}
            autoCapitalize="characters"
            autoCorrect={false}
          />

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
export default function NewCourierClaimScreen() {
  const { parcelCode } = useLocalSearchParams<{ parcelCode?: string }>();
  return <NewCourierClaimScreenContent key={useFreshKey(parcelCode)} />;
}
