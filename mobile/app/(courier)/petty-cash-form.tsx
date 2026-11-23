import { CameraView, useCameraPermissions } from 'expo-camera';
import * as Haptics from 'expo-haptics';
import { useLocalSearchParams, useRouter } from 'expo-router';
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
import { usePettyCash, useSavePettyCash } from '@/lib/petty-cash';
import { newKey } from '@/lib/queue';
import { describeActionError } from '@/lib/use-action';
import { useFreshKey } from '@/lib/use-fresh-key';
import { Button, Chip, Field } from '@/ui';

/**
 * NOUVELLE DÉPENSE / MODIFIER UN BROUILLON — D-168.
 *
 * Libellé, catégorie, montant, photo du ticket si on l'a. La date, l'agence et
 * l'acheteur ne se saisissent pas : le serveur pose le jour de la société,
 * l'agence du livreur et le livreur lui-même. La dépense part en brouillon.
 *
 * La clé d'idempotence est tirée UNE fois par ouverture : un second appui
 * pendant l'envoi, ou un rejeu après un délai réseau, rend la même dépense.
 */
function PettyCashFormContent({ id }: { id: string | null }) {
  const { t } = useI18n();
  const router = useRouter();
  const pettyCash = usePettyCash();
  const save = useSavePettyCash();

  const current = id ? pettyCash.data?.items.find((item) => item.id === id) : undefined;
  const categories = pettyCash.data?.categories ?? [];

  const [idempotencyKey] = useState(() => newKey('petty'));
  const [label, setLabel] = useState(current?.label ?? '');
  const [category, setCategory] = useState(current?.category ?? '');
  const [amount, setAmount] = useState(current ? String(current.amount / 100) : '');
  const [photo, setPhoto] = useState<string | null>(null);
  const [note, setNote] = useState<string | null>(null);

  const [permission, requestPermission] = useCameraPermissions();
  const cameraRef = useRef<CameraView>(null);
  const [camera, setCamera] = useState(false);

  const value = Number(amount.replace(',', '.'));
  const ready = label.trim() !== '' && category !== '' && Number.isFinite(value) && value > 0;

  const capture = async () => {
    const shot = await cameraRef.current?.takePictureAsync({
      quality: 0.5,
      base64: true,
      exif: false,
    });

    setCamera(false);
    if (shot?.base64) setPhoto(shot.base64);
  };

  const submit = () => {
    if (!ready || save.isPending) return;

    save.mutate(
      {
        id,
        idempotencyKey,
        body: {
          label: label.trim(),
          category,
          amount: Math.round(value * 100) / 100,
          proof: photo
            ? { name: `ticket-${Date.now()}.jpg`, mimeType: 'image/jpeg', content: photo }
            : null,
        },
      },
      {
        onSuccess: () => {
          void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
          router.back();
        },
        onError: (error) => {
          void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error);
          setNote(describeActionError(error, t));
        },
      },
    );
  };

  // Une catégorie retirée depuis reste proposée sur le brouillon qui la porte.
  const choices =
    current?.category && !categories.includes(current.category)
      ? [...categories, current.category]
      : categories;

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
          <ArrowLeft size={24} className="flip-rtl text-foreground" />
        </Pressable>
        <Text className="flex-1 text-[20px] font-bold text-foreground">
          {id ? t('pettyCash.editTitle') : t('pettyCash.new')}
        </Text>
      </View>

      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        className="flex-1"
      >
        <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-4 px-5 pb-6">
          <Text className="text-[14px] text-muted-foreground">{t('pettyCash.hint')}</Text>

          <Field label={t('pettyCash.label')} value={label} onChangeText={setLabel} />

          <View className="gap-2">
            <Text className="text-[15px] font-medium text-foreground">
              {t('pettyCash.category')}
            </Text>
            <View className="flex-row flex-wrap gap-2">
              {choices.map((choice) => (
                <Chip
                  key={choice}
                  label={choice}
                  active={category === choice}
                  onPress={() => setCategory(choice)}
                />
              ))}
            </View>
          </View>

          <Field
            label={t('pettyCash.amount')}
            value={amount}
            onChangeText={setAmount}
            inputMode="decimal"
          />

          <Button
            label={photo ? t('parcel.photoTaken') : t('pettyCash.photo')}
            icon={photo ? Check : Camera}
            tone="ghost"
            onPress={() => {
              if (!permission?.granted) {
                void requestPermission();
                return;
              }
              setCamera(true);
            }}
            full
          />
          {current?.proofName && !photo ? (
            <Text className="text-[14px] text-muted-foreground">
              {t('pettyCash.currentProof', { name: current.proofName })}
            </Text>
          ) : null}

          {note ? <Text className="text-[15px] text-destructive">{note}</Text> : null}
        </ScrollView>

        <View className="px-5 pb-4">
          <Button
            label={t('common.save')}
            onPress={submit}
            disabled={!ready}
            busy={save.isPending}
            full
          />
        </View>
      </KeyboardAvoidingView>

      <Modal visible={camera} animationType="slide" onRequestClose={() => setCamera(false)}>
        <View className="flex-1 bg-black">
          <CameraView ref={cameraRef} style={{ flex: 1 }} facing="back" />
          <View className="gap-3 bg-black px-5 pb-10 pt-4">
            <Button label={t('pettyCash.photo')} onPress={() => void capture()} full />
            <Button label={t('common.cancel')} tone="ghost" onPress={() => setCamera(false)} full />
          </View>
        </View>
      </Modal>
    </SafeAreaView>
  );
}

/** Le formulaire repart vide à chaque ouverture — `useFreshKey` (D-149). */
export default function PettyCashFormScreen() {
  const { id } = useLocalSearchParams<{ id?: string }>();
  return <PettyCashFormContent key={useFreshKey(id)} id={id ?? null} />;
}
