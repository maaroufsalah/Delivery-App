import { StatusBar } from 'expo-status-bar';
import { useTenantTheme } from '@/theme/tenant-theme';

/**
 * La barre système, accordée au thème de l'APPLICATION — C9 porté sur mobile.
 *
 * `style="auto"` suit le schéma de l'OS. Depuis que l'application ne le suit
 * plus (clair par défaut, bascule manuelle dans Profil), les deux peuvent
 * diverger : sur un téléphone en sombre, la barre choisirait du texte clair
 * au-dessus d'une application claire, et l'heure deviendrait illisible.
 *
 * `style` décrit le CONTENU de la barre, pas son fond : sur un thème clair il
 * faut du texte sombre, et l'inverse.
 *
 * Un composant à part parce qu'il doit être ENFANT du fournisseur de thème —
 * la racine, elle, en est le parent.
 */
export function ThemedStatusBar() {
  const { scheme } = useTenantTheme();

  return <StatusBar style={scheme === 'dark' ? 'light' : 'dark'} />;
}
