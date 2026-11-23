import type { LucideIcon } from 'lucide-react-native';
import type { ReactNode } from 'react';
import { Modal, Pressable, ScrollView, Text, useWindowDimensions, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

/**
 * La FEUILLE qui monte du bas — M4 bloc 1.
 *
 * **Pourquoi pas une bibliothèque.** `@gorhom/bottom-sheet` et consorts
 * reposent sur `react-native-gesture-handler`, qui n'est PAS lié dans l'APK
 * `ebb98c4b` : l'ajouter exigerait un nouveau binaire, donc un passage par le
 * magasin, donc l'inverse d'une livraison OTA. La feuille est donc bâtie sur le
 * `Modal` du cœur de React Native.
 *
 * **C'EST LA FENÊTRE QUI S'ANIME, PAS LA FEUILLE — hotfix Samsung, D-212.**
 * Les animations de layout Reanimated (`entering={SlideInDown}`, `FadeIn`)
 * figeaient la géométrie du PREMIER rendu du Modal, fait sur une taille
 * estimée ; la vraie taille du dialogue arrive pendant l'animation et était
 * écrasée. Sur Android 15 bord à bord (Galaxy S24/S25, A05), la feuille restait
 * dessinée plus haut que là où React la mesure : l'appui sur le texte était
 * perdu dès que le doigt bougeait d'un pixel. `animationType="fade"` anime la
 * fenêtre native sans toucher au cadre d'aucune vue : la position dessinée est
 * la position mesurée. Aucune animation de layout dans ce fichier.
 *
 * **Ce qu'on y perd, et pourquoi c'est acceptable** : pas de glissement au
 * doigt pour refermer, pas de points d'accroche intermédiaires. Un menu et un
 * choix de statut n'en ont pas besoin — on touche une ligne, ou on touche à
 * côté. Le voile est une cible de fermeture pleine page, ce qui est plus facile
 * à viser qu'une poignée.
 *
 * **ELLE DÉFILE — retouches Anass, point 6.** « La liste des statuts doit
 * scroller entièrement. » Le contenu était posé dans une `View` sans hauteur :
 * la feuille grandissait sous l'écran, et les dix derniers statuts d'un colis
 * en cours de livraison — il y en a seize — étaient hors d'atteinte. Aucune
 * bibliothèque à ajouter pour cela : une hauteur maximale et un `ScrollView`
 * du cœur de React Native suffisent, et l'OTA reste possible.
 *
 * La borne est une FRACTION de l'écran, pas un nombre de points : un petit
 * téléphone doit garder du voile à toucher pour refermer, et un grand doit
 * pouvoir montrer la liste entière sans défiler du tout.
 */

/** Part de la hauteur d'écran au-delà de laquelle le contenu défile. */
const MAX_HEIGHT_RATIO = 0.72;
export function Sheet({
  open,
  onClose,
  title,
  children,
}: {
  open: boolean;
  onClose: () => void;
  title?: string;
  children: ReactNode;
}) {
  const insets = useSafeAreaInsets();
  const { height } = useWindowDimensions();

  return (
    <Modal
      visible={open}
      transparent
      animationType="fade"
      onRequestClose={onClose}
      statusBarTranslucent
    >
      <View className="flex-1">
        {/*
          Le voile. `expo-blur` n'est pas lié : un noir à 45 % fait le même
          travail de mise en retrait, et il est lisible dans les deux modes.
        */}
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={title}
          onPress={onClose}
          className="flex-1"
          style={{ backgroundColor: 'rgba(0,0,0,0.45)' }}
        />

        <View
          className="absolute inset-x-0 bottom-0 rounded-t-3xl border-t border-border bg-card"
          style={{ paddingBottom: insets.bottom + 12 }}
        >
          {/* La poignée ne sert à rien fonctionnellement : elle DIT que ça se
              ferme, et c'est la seule affordance dont la feuille dispose. */}
          <View className="items-center py-2.5">
            <View className="h-1 w-10 rounded-full bg-border" />
          </View>

          {title ? (
            <Text className="px-5 pb-2 text-[18px] font-semibold text-foreground">{title}</Text>
          ) : null}

          {/*
            `ScrollView` et non `FlatList` : le contenu d'une feuille est un
            menu, pas une liste de mille lignes, et `FlatList` imbriqué dans un
            conteneur qui défile est précisément ce que React Native déconseille.

            `alwaysBounceVertical={false}` : sans lui, une feuille courte
            rebondit au doigt comme si elle cachait quelque chose.
          */}
          <ScrollView
            keyboardShouldPersistTaps="handled"
            className="px-3 pb-1"
            style={{ maxHeight: height * MAX_HEIGHT_RATIO }}
            contentContainerClassName="pb-1"
            alwaysBounceVertical={false}
            showsVerticalScrollIndicator
          >
            {children}
          </ScrollView>
        </View>
      </View>
    </Modal>
  );
}

/** Une ligne de feuille : icône, libellé, et de quoi la toucher sans viser. */
export function SheetItem({
  icon: Icon,
  label,
  hint,
  tone = 'default',
  onPress,
}: {
  icon: LucideIcon;
  label: string;
  hint?: string;
  tone?: 'default' | 'danger';
  onPress: () => void;
}) {
  const color = tone === 'danger' ? 'text-destructive' : 'text-foreground';

  return (
    <Pressable
      accessibilityRole="button"
      onPress={onPress}
      className="min-h-[52px] flex-row items-center gap-3 rounded-2xl px-3"
      style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
    >
      <Icon
        size={20}
        className={tone === 'danger' ? 'text-destructive' : 'text-muted-foreground'}
      />
      <View className="flex-1">
        <Text className={`text-[16px] ${color}`}>{label}</Text>
        {hint ? <Text className="text-[13px] text-muted-foreground">{hint}</Text> : null}
      </View>
    </Pressable>
  );
}
