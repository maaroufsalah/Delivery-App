import { useRouter } from 'expo-router';
import {
  Boxes,
  ClipboardList,
  MessageSquare,
  PackagePlus,
  Plus,
  Truck,
  type LucideIcon,
} from 'lucide-react-native';
import { useState } from 'react';
import { Pressable, View } from 'react-native';
import { useI18n, type MessageKey } from '@/i18n';
import { Divider, Sheet, SheetItem } from '@/ui';

/**
 * LE BOUTON « + » — V2 vendeur bloc 1, spécification §16.
 *
 * **Pourquoi il remplace l'onglet central.** La barre servait « Créer un
 * colis » comme cinquième destination, en relief au milieu. La spécification
 * demande cinq destinations — Accueil, Colis, Bons, Warehouse, Plus — et six
 * actions rapides. Garder « Créer » dans la barre aurait coûté un des cinq
 * pouces à une ACTION, alors que les quatre autres sont des lieux.
 *
 * **Il flotte au-dessus de la barre, pas dedans.** Un bouton d'action reste
 * atteignable sur tous les écrans de l'espace sans occuper une cible de
 * navigation, et c'est le geste que tout le monde connaît.
 *
 * **Il ne propose que ce que le téléphone sait faire.** L'import Excel est
 * absent de la feuille : choisir un classeur, lire ses erreurs ligne par ligne
 * et corriger la saisie est un travail d'écran large, et la spécification le
 * demande explicitement côté web (§3). Une entrée qui ouvrirait un sélecteur de
 * fichiers pour échouer ensuite serait pire que pas d'entrée.
 *
 * Les deux actions de bon sont ouvertes au bloc 4 et l'ajout de produit au
 * bloc 7 : la feuille ne montre aujourd'hui que ce qui a un écran derrière.
 */

interface QuickAction {
  key: MessageKey;
  icon: LucideIcon;
  href: string;
}

const ACTIONS: QuickAction[] = [
  { key: 'quick.newParcel', icon: PackagePlus, href: '/(merchant)/new' },
  { key: 'quick.newPickup', icon: Truck, href: '/(merchant)/pickup' },
  { key: 'quick.newDropoff', icon: ClipboardList, href: '/(merchant)/pickup?kind=dropoff' },
  { key: 'quick.newClaim', icon: MessageSquare, href: '/(merchant)/claim/new' },
  { key: 'quick.newProduct', icon: Boxes, href: '/(merchant)/product/new' },
];

export function MerchantQuickActions() {
  const { t } = useI18n();
  const router = useRouter();
  const [open, setOpen] = useState(false);

  return (
    <>
      {/*
        `bottom-[84px]` : la barre basse fait 56 points, plus la marge de sûreté
        du bas sur les téléphones à encoche. Le bouton se pose juste au-dessus,
        côté fin de ligne — donc à gauche en arabe, sans une ligne de plus
        (utilitaire logique `end-`).
      */}
      <View className="absolute bottom-[84px] end-5 z-10">
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('quick.title')}
          onPress={() => setOpen(true)}
          className="h-14 w-14 items-center justify-center rounded-full bg-primary"
          style={({ pressed }) => (pressed ? { opacity: 0.8 } : null)}
        >
          <Plus size={26} className="text-primary-foreground" />
        </Pressable>
      </View>

      <Sheet open={open} onClose={() => setOpen(false)} title={t('quick.title')}>
        <View className="gap-1 px-2 pb-2">
          {/* `SheetItem` et non une ligne écrite à la main : c'est le
              vocabulaire de feuille de l'application, et une seconde version
              aurait divergé au premier ajustement de hauteur. */}
          {ACTIONS.map((action, index) => (
            <View key={action.key}>
              {index > 0 ? <Divider /> : null}
              <SheetItem
                icon={action.icon}
                label={t(action.key)}
                onPress={() => {
                  setOpen(false);
                  router.push(action.href);
                }}
              />
            </View>
          ))}
        </View>
      </Sheet>
    </>
  );
}
