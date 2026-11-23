import { Text, View } from 'react-native';
import { useTokens } from '@/theme/tenant-theme';

export interface TimelineEntry {
  id: string;
  /** Le libellé du statut, déjà traduit par l'appelant. */
  label: string;
  /** La couleur du statut — `packages/shared/statuses.ts`, jamais d'ici. */
  color: string;
  at: string;
  author?: string | null;
  comment?: string | null;
}

/**
 * L'HISTORIQUE d'un colis, du plus récent au plus ancien — M4 bloc 3.
 *
 * **Le plus récent en haut.** C'est l'ordre que l'API rend (`events` trié
 * `desc`), et c'est l'ordre utile : celui qui ouvre la fiche veut savoir où en
 * est le colis, pas d'où il vient. Le pastillage marque la première entrée, qui
 * est l'état courant.
 *
 * **Le trait est dessiné par des vues, pas par un SVG.** Une timeline est une
 * liste de hauteurs variables ; un tracé vectoriel devrait mesurer chaque ligne
 * pour se dessiner. Deux `View` — un point, un filet qui remplit le reste — font
 * le même rendu et suivent le contenu tout seuls.
 */
export function Timeline({ entries }: { entries: TimelineEntry[] }) {
  const tokens = useTokens();

  return (
    <View>
      {entries.map((entry, index) => {
        const last = index === entries.length - 1;
        const current = index === 0;

        return (
          <View key={entry.id} className="flex-row gap-3">
            <View className="items-center" style={{ width: 16 }}>
              <View
                className="rounded-full"
                style={{
                  width: current ? 14 : 10,
                  height: current ? 14 : 10,
                  marginTop: current ? 3 : 5,
                  backgroundColor: entry.color,
                  // L'anneau ne se voit que sur l'entrée courante : c'est ce
                  // qui la fait ressortir sans ajouter de texte.
                  borderWidth: current ? 3 : 0,
                  borderColor: `${entry.color}33`,
                }}
              />
              {last ? null : (
                <View className="w-px flex-1" style={{ backgroundColor: tokens.border }} />
              )}
            </View>

            <View className={`flex-1 ${last ? 'pb-1' : 'pb-4'}`}>
              <Text
                className="text-[16px] font-semibold"
                style={{ color: current ? entry.color : tokens.foreground }}
              >
                {entry.label}
              </Text>
              <Text className="text-[13px] text-muted-foreground">
                {entry.at}
                {entry.author ? ` · ${entry.author}` : ''}
              </Text>
              {entry.comment ? (
                <Text className="mt-1 text-[15px] text-foreground">{entry.comment}</Text>
              ) : null}
            </View>
          </View>
        );
      })}
    </View>
  );
}
