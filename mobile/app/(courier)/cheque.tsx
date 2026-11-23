import { CameraView, useCameraPermissions } from 'expo-camera';
import * as Haptics from 'expo-haptics';
import { useRouter } from 'expo-router';
import { useFreshKey } from '@/lib/use-fresh-key';
import { ArrowLeft, Camera, Check } from 'lucide-react-native';
import { useRef, useState } from 'react';
import {
  KeyboardAvoidingView,
  Modal,
  Platform,
  Pressable,
  ScrollView,
  Text,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useI18n } from '@/i18n';
import { describeActionError } from '@/lib/use-action';
import { ApiError } from '@/lib/api';
import { useCheque } from '@/lib/courier';
import { Button, Field } from '@/components/ui';

/**
 * MON CHÈQUE — M1, prolongement de C11 bloc 7 (D-067).
 *
 * Le livreur sort du comptoir avec un bout de papier. Il le photographie et
 * recopie ce qu'il lit dessus : numéro, banque, date, montant.
 *
 * **Aucun relevé à choisir.** L'API retrouve le sien — le plus récemment réglé
 * par chèque — parce que lui faire choisir parmi une liste de `PAI-…` debout
 * dans une agence n'aurait aucun sens, et qu'il n'en a de toute façon qu'un en
 * attente à la fois.
 *
 * **La photo d'abord, la saisie ensuite.** C'est l'ordre du geste réel : on
 * prend la photo pendant qu'on a le chèque en main, on tape les chiffres après,
 * en les relisant sur l'écran.
 */
function ChequeScreenContent() {
  const { t } = useI18n();
  const router = useRouter();
  const cheque = useCheque();

  const [permission, requestPermission] = useCameraPermissions();
  const cameraRef = useRef<CameraView>(null);
  const [camera, setCamera] = useState(false);

  const [photo, setPhoto] = useState<string | null>(null);
  const [number, setNumber] = useState('');
  const [bank, setBank] = useState('');
  const [date, setDate] = useState('');
  const [amount, setAmount] = useState('');
  const [note, setNote] = useState<string | null>(null);

  const capture = async () => {
    const shot = await cameraRef.current?.takePictureAsync({
      quality: 0.5,
      base64: true,
      // Un chèque photographié embarquerait sinon les coordonnées EXIF de
      // l'agence — une donnée qu'on n'a pas demandé à collecter.
      exif: false,
    });

    setCamera(false);
    if (shot?.base64) setPhoto(shot.base64);
  };

  const submit = () => {
    if (!photo) return;

    cheque.mutate(
      {
        chequeNumber: number.trim(),
        chequeBank: bank.trim(),
        chequeDate: date.trim(),
        amount: amount ? Number(amount) : undefined,
        proof: {
          name: `cheque-${number.trim() || 'sans-numero'}.jpg`,
          mimeType: 'image/jpeg',
          content: photo,
        },
      },
      {
        onSuccess: () => {
          void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
          setNote(t('cheque.sent'));
          router.back();
        },
        onError: (error) => {
          void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error);
          setNote(
            error instanceof ApiError && error.reason === 'no-cheque-statement'
              ? t('cheque.none')
              : describeActionError(error, t),
          );
        },
      },
    );
  };

  return (
    <SafeAreaView className="flex-1 bg-background" edges={['top']}>
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
        <Text className="flex-1 text-[20px] font-bold text-foreground">{t('cheque.title')}</Text>
      </View>

      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        className="flex-1"
      >
        <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-4 px-5 pb-6">
          <Button
            label={photo ? t('parcel.photoTaken') : t('cheque.take')}
            icon={photo ? Check : Camera}
            tone={photo ? 'ghost' : 'primary'}
            onPress={() => {
              if (!permission?.granted) {
                void requestPermission();
                return;
              }
              setCamera(true);
            }}
            full
          />

          <Field
            label={t('cheque.number')}
            value={number}
            onChangeText={setNumber}
            autoCapitalize="characters"
            autoCorrect={false}
          />
          <Field label={t('cheque.bank')} value={bank} onChangeText={setBank} />
          <Field
            label={t('cheque.date')}
            value={date}
            onChangeText={setDate}
            placeholder="2026-09-25"
            autoCapitalize="none"
          />
          <Field
            label={t('cheque.amount')}
            value={amount}
            onChangeText={setAmount}
            inputMode="decimal"
          />

          {note ? <Text className="text-[15px] text-muted-foreground">{note}</Text> : null}
        </ScrollView>

        <View className="px-5 pb-4">
          <Button
            label={t('common.save')}
            onPress={submit}
            // La photo est ce qui fait la preuve : sans elle, la saisie seule
            // ne documente rien.
            disabled={!photo || number.trim() === ''}
            busy={cheque.isPending}
            full
          />
        </View>
      </KeyboardAvoidingView>

      <Modal visible={camera} animationType="slide" onRequestClose={() => setCamera(false)}>
        <View className="flex-1 bg-black">
          <CameraView ref={cameraRef} style={{ flex: 1 }} facing="back" />
          <View className="gap-3 bg-black px-5 pb-10 pt-4">
            <Button label={t('cheque.take')} onPress={() => void capture()} full />
            <Button label={t('common.cancel')} tone="ghost" onPress={() => setCamera(false)} full />
          </View>
        </View>
      </Modal>
    </SafeAreaView>
  );
}

/** Coque Recette 7 (point 5) : le contenu repart vide à chaque fermeture — `useFreshKey`. */
export default function ChequeScreen() {
  return <ChequeScreenContent key={useFreshKey()} />;
}
