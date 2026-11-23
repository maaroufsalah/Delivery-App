import { CameraView, useCameraPermissions } from 'expo-camera';
import * as Haptics from 'expo-haptics';
import { useRouter } from 'expo-router';
import { ScanLine } from 'lucide-react-native';
import { useRef, useState } from 'react';
import { Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useI18n } from '@/i18n';
import { Button } from '@/ui';

/** Anti-rebond : le lecteur rend le même code dix fois par seconde. */
const RESCAN_LOCK_MS = 1_200;

/**
 * LE SCANNER DU COMPTOIR — M3 bloc 7.
 *
 * **Il n'écrit rien.** Il OUVRE la fiche. C'est toute la différence avec celui
 * du livreur, qui compose un circuit par le scan (D-073) : à l'agence, on
 * scanne pour savoir — recevoir un colis, vérifier où il en est, le passer au
 * statut qui convient. Le geste d'écriture est ensuite choisi sur la fiche,
 * dans la liste que la machine à états autorise.
 *
 * Faire l'inverse — scanner pour réceptionner d'un coup — aurait été plus
 * rapide et beaucoup plus dangereux : un colis passé en « reçu en agence » par
 * erreur se répare à la main, et la trace reste.
 */
export default function StaffScanScreen() {
  const { t } = useI18n();
  const router = useRouter();

  const [permission, requestPermission] = useCameraPermissions();
  const lastScan = useRef(0);
  const [seen, setSeen] = useState<string | null>(null);

  const onScan = (code: string) => {
    const now = Date.now();
    if (now - lastScan.current < RESCAN_LOCK_MS) return;
    lastScan.current = now;

    void Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Medium);
    setSeen(code);
    router.push(`/(staff)/parcel/${encodeURIComponent(code)}`);
  };

  if (!permission?.granted) {
    return (
      <SafeAreaView className="flex-1 justify-center gap-4 bg-background px-6">
        <Text className="text-center text-[16px] text-foreground">{t('scan.permission')}</Text>
        <Button label={t('scan.permissionAsk')} onPress={() => void requestPermission()} full />
      </SafeAreaView>
    );
  }

  return (
    <View className="flex-1 bg-black">
      <CameraView
        style={{ flex: 1 }}
        facing="back"
        barcodeScannerSettings={{
          barcodeTypes: ['qr', 'code128', 'code39', 'ean13', 'ean8', 'itf14', 'upc_a'],
        }}
        onBarcodeScanned={({ data }) => onScan(data)}
      />

      <SafeAreaView edges={['bottom']} className="bg-black">
        <View className="items-center gap-2 px-5 py-4">
          <ScanLine size={20} className="text-primary-foreground" />
          <Text className="text-center text-[15px] text-primary-foreground">
            {seen ?? t('scan.hint')}
          </Text>
        </View>
      </SafeAreaView>
    </View>
  );
}
