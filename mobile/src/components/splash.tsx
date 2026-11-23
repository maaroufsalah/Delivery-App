import { ActivityIndicator, View } from 'react-native';

/** L'écran d'attente de la racine : rien d'autre qu'un indicateur centré. */
export function Splash() {
  return (
    <View className="flex-1 items-center justify-center bg-background">
      <ActivityIndicator size="large" />
    </View>
  );
}
