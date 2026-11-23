import { Bell } from 'lucide-react-native';
import { Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { useAnnouncements } from '@/lib/courier';
import { formatStamp } from '@/lib/datetime';
import { Divider, EmptyState, Sheet } from '@/ui';

/**
 * LES ACTUALITÉS, derrière la cloche — L2 bloc 1, maquette 2.
 *
 * **Ce qu'elle déplace.** M4 posait les actualités en BAS de l'accueil, sous
 * l'activité récente, et le menu général n'y renvoyait pas — « un menu qui
 * renvoie à l'écran qu'on vient de quitter est un aller-retour pour rien ».
 * La maquette d'Anass ne montre pas cette section : elle montre une **cloche**
 * en haut à droite. Le contenu ne disparaît donc pas, il passe derrière la
 * cloche, et l'accueil se termine sur l'activité récente — ce qui rapproche du
 * modèle qu'un livreur connaît déjà de toutes les autres applications.
 *
 * **Aucun appel de plus.** La requête est celle de l'accueil, servie par le
 * cache de TanStack Query : la feuille s'ouvre sur des données déjà là.
 *
 * **Elle se tait proprement quand la route n'existe pas** : `/announcements`
 * est une commodité, pas un dû, et `useAnnouncements` ne réessaie pas.
 */
export function NewsSheet({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t, locale } = useI18n();
  const news = useAnnouncements(10);

  const items = news.data ?? [];

  // Date et heure à la seconde, comme sur le web (D-254).
  const when = (iso: string | null) => formatStamp(iso, locale);

  return (
    <Sheet open={open} onClose={onClose} title={t('menu.news')}>
      {items.length === 0 ? (
        <View className="px-2 pb-2 pt-1">
          <EmptyState art="route" title={t('news.empty')} hint={t('news.emptyHint')} />
        </View>
      ) : (
        <View className="max-h-[420px]">
          {items.map((item, index) => (
            <View key={item.id}>
              {index > 0 ? <Divider /> : null}
              <View className="flex-row items-start gap-3 px-3 py-3">
                <Bell size={18} className="text-primary" />
                <View className="flex-1 gap-1">
                  <Text className="text-[16px] font-semibold text-foreground">{item.title}</Text>
                  {item.intro ? (
                    <Text className="text-[14px] text-muted-foreground">{item.intro}</Text>
                  ) : null}
                  <Text className="text-[12px] text-muted-foreground">
                    {when(item.publishedAt ?? item.createdAt)}
                  </Text>
                </View>
              </View>
            </View>
          ))}
        </View>
      )}
    </Sheet>
  );
}
