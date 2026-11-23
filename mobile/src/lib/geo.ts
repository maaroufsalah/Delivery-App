import * as Location from 'expo-location';

/**
 * LA POSITION, SI ELLE EST DISPONIBLE — M1, D-074 ; sortie de la fiche du
 * colis pour le chantier « Livrer en un appui » (D-156), où trois écrans
 * rapportent une issue.
 *
 * Jamais demandée en bloquant : une permission refusée ou un GPS muet ne
 * doivent pas empêcher de rapporter une livraison. Trois secondes au plus, puis
 * le rapport part sans position.
 */
export async function lastKnownCoords(): Promise<
  { latitude: number; longitude: number } | undefined
> {
  const lookup = (async () => {
    try {
      const granted = await Location.getForegroundPermissionsAsync();
      if (granted.status !== 'granted') return undefined;
      const position = await Location.getLastKnownPositionAsync();
      return position
        ? { latitude: position.coords.latitude, longitude: position.coords.longitude }
        : undefined;
    } catch {
      // Sans position : le rapport part quand même. C'est la règle.
      return undefined;
    }
  })();

  return Promise.race([
    lookup,
    new Promise<undefined>((resolve) => setTimeout(() => resolve(undefined), 3_000)),
  ]);
}
