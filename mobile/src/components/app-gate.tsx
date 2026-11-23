import type { MobileConfig } from '@delivery/shared';
import Constants from 'expo-constants';
import { CloudOff, RefreshCw } from 'lucide-react-native';
import { type ReactNode, useEffect, useState } from 'react';
import { Linking, Platform, View } from 'react-native';
import { useI18n } from '@/i18n';
import { api } from '@/lib/api';
import { Button, EmptyState } from './ui';
import { Splash } from './splash';

/**
 * Le portail : version minimale et maintenance — M1, docs/DECISIONS.md D-070.
 *
 * **Vérifié AVANT d'afficher quoi que ce soit.** Une version qui parle un
 * contrat disparu ne casse pas au lancement, elle casse plus loin — sur un
 * geste de terrain, quand le livreur est devant une porte. Mieux vaut l'arrêter
 * tout de suite, avec un bouton qui mène au magasin.
 *
 * **Sans bouton « continuer ».** Un plancher de version est posé quand
 * l'ancienne ne marche plus ; le contourner ne ferait que déplacer la panne.
 *
 * **Une vérification qui ÉCHOUE laisse passer.** Si l'API ne répond pas, ce
 * n'est pas une raison de bloquer : le livreur peut travailler hors ligne sur
 * sa tournée en cache (D-077), et l'enfermer dans un écran d'erreur lui
 * retirerait le seul outil qu'il a.
 */
export function AppGate({ children }: { children: ReactNode }) {
  const { t } = useI18n();
  const [config, setConfig] = useState<MobileConfig | null>(null);
  const [checked, setChecked] = useState(false);

  useEffect(() => {
    void (async () => {
      try {
        setConfig(await api<MobileConfig>('/public/mobile-config', { anonymous: true }));
      } catch {
        // Réseau absent ou société pas encore choisie : on laisse passer.
        setConfig(null);
      } finally {
        setChecked(true);
      }
    })();
  }, []);

  if (!checked) return <Splash />;

  if (config?.maintenance) {
    return (
      <View className="flex-1 bg-background">
        <EmptyState icon={CloudOff} title={t('app.maintenance')} hint={config.maintenance} />
      </View>
    );
  }

  if (config && isBelowMinimum(appVersion(), config.minVersion)) {
    const url = Platform.OS === 'ios' ? config.storeUrls.ios : config.storeUrls.android;

    return (
      <View className="flex-1 justify-center gap-6 bg-background px-8">
        <EmptyState icon={RefreshCw} title={t('app.updateRequired')} hint={t('app.updateHint')} />
        {url ? (
          <Button label={t('app.update')} onPress={() => void Linking.openURL(url)} full />
        ) : null}
      </View>
    );
  }

  return <>{children}</>;
}

/** La version déclarée dans `app.json`, celle qu'EAS incrémente. */
function appVersion(): string {
  return Constants.expoConfig?.version ?? '0.0.0';
}

/**
 * Comparaison `x.y.z`, segment par segment.
 *
 * Pas de `localeCompare` ni de comparaison de chaînes : `1.10.0` est plus
 * récent que `1.9.0`, et l'ordre alphabétique dit l'inverse. C'est la forme
 * exacte du bug qui bloque toute une flotte sur une version parfaitement à jour.
 *
 * Un plancher VIDE ne bloque rien : c'est le défaut de la plateforme.
 */
export function isBelowMinimum(current: string, minimum: string): boolean {
  if (!minimum.trim()) return false;

  const parse = (value: string) => value.split('.').map((part) => Number.parseInt(part, 10) || 0);
  const [a, b] = [parse(current), parse(minimum)];

  for (let index = 0; index < Math.max(a.length, b.length); index += 1) {
    const left = a[index] ?? 0;
    const right = b[index] ?? 0;

    if (left !== right) return left < right;
  }

  return false;
}
