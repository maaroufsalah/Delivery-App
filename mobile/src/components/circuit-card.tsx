import { routeTargetsSender, type CourierRoute } from '@delivery/shared';
import { useRouter } from 'expo-router';
import { ChevronRight, PackageSearch, Truck, Undo2 } from 'lucide-react-native';
import { Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { Card, ProgressBar } from '@/ui';

/**
 * UNE carte de circuit — le bloc que la maquette 5 répète deux fois.
 *
 * Le type décide du libellé, de l'icône et du décompte : « 5 points » pour un
 * ramassage, « 8 livraisons » pour une livraison. Le même mot pour les deux
 * aurait été plus simple à écrire et plus pauvre à lire — un livreur ne prépare
 * pas sa moto de la même façon dans les deux cas.
 *
 * Le RETOUR (lot C, point C2, D-179) a la même carte, sous le menu Retours :
 * son code, « 4 colis à rendre », « X sur Y traités », « Voir le détail ».
 */
export function CircuitCard({ route }: { route: CourierRoute }) {
  const { t } = useI18n();
  const router = useRouter();

  const toSender = routeTargetsSender(route.kind);
  const isReturn = route.kind === 'return';
  const done = route.parcelCount - route.remainingCount;
  /*
   * « 150/200 ramassés » — Recette 5, points 7 et 8 : le total d'un ramassage
   * compte aussi les colis retirés de ses bons (non remis).
   */
  const removed = route.notes.reduce((sum, note) => sum + note.removedCount, 0);
  const total = route.parcelCount + removed;

  return (
    <Card
      className="gap-3"
      onPress={() => router.push(`/(courier)/circuit/${encodeURIComponent(route.code)}`)}
    >
      <View className="flex-row items-center gap-3">
        <View
          className={`h-12 w-12 items-center justify-center rounded-2xl ${
            isReturn ? 'bg-warning/15' : toSender ? 'bg-success/15' : 'bg-primary/15'
          }`}
        >
          {isReturn ? (
            <Undo2 size={24} className="flip-rtl text-warning" />
          ) : toSender ? (
            <PackageSearch size={24} className="text-success" />
          ) : (
            <Truck size={24} className="text-primary" />
          )}
        </View>

        <View className="flex-1 gap-0.5">
          <Text className="text-[17px] font-semibold text-foreground">
            {t(isReturn ? 'circuits.return' : toSender ? 'circuits.pickup' : 'circuits.delivery')}
          </Text>
          <Text className="text-[14px] text-muted-foreground">
            {isReturn
              ? `${route.code} · ${t('circuits.returnParcels', { count: route.parcelCount })}`
              : t(toSender ? 'circuits.points' : 'circuits.deliveries', {
                  count: route.parcelCount,
                })}
            {route.status === 'draft' ? ` · ${t('circuits.draft')}` : ''}
          </Text>
        </View>

        <ChevronRight size={20} className="flip-rtl text-muted-foreground" />
      </View>

      {/* La progression, seulement quand le circuit est parti : un brouillon
          n'a rien à montrer d'autre que ce qu'on vient d'y poser. */}
      {route.status === 'active' ? (
        <View className="gap-1.5">
          <ProgressBar value={done} total={total} />
          <Text className="text-[13px] text-muted-foreground">
            {isReturn
              ? t('circuits.processed', { done, total: route.parcelCount })
              : toSender
                ? t('circuits.pickedProgress', { done, total, remaining: route.remainingCount })
                : t('circuits.deliveredProgress', { done, total, remaining: route.remainingCount })}
          </Text>
        </View>
      ) : null}

      <Text className="text-[15px] font-medium text-primary">{t('circuits.detail')}</Text>
    </Card>
  );
}
