import * as Haptics from 'expo-haptics';
import type { LucideIcon } from 'lucide-react-native';
import { Pressable, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTokens } from '@/theme/tenant-theme';

export interface BottomBarItem {
  key: string;
  label: string;
  icon: LucideIcon;
  /** Le badge numérique. `0` et `undefined` ne s'affichent pas. */
  badge?: number;
  /** L'entrée MISE EN AVANT — le scanner, au centre. */
  raised?: boolean;
  onPress: () => void;
}

/**
 * La barre de navigation basse — M4 bloc 2.
 *
 * **Pourquoi elle remplace `<Tabs>` d'expo-router.** Le composant d'onglets ne
 * sait pas mettre une entrée en relief, pose ses badges à sa façon, et n'accepte
 * ses couleurs que par un objet de style — d'où le `'var(--primary)'` de M1,
 * que React Native ne résout pas : la teinte active était donc *absente*, pas
 * seulement approximative. Une barre écrite ici lit les jetons, porte le bouton
 * central, et les écrans restent des routes ordinaires.
 *
 * **Ancrée à 100 % du bas**, la zone sûre comprise dans le rembourrage plutôt
 * qu'au-dessus : sur un iPhone, une barre qui « flotte » au-dessus de la barre
 * gestuelle laisse une bande de fond visible que personne ne sait expliquer.
 *
 * **Retour haptique à chaque changement d'onglet**, et à lui seul : ré-appuyer
 * sur l'onglet courant ne vibre pas, sinon le geste perd son sens de « quelque
 * chose a changé ».
 */
export function BottomBar({
  items,
  active,
  raisedSize = 56,
}: {
  items: BottomBarItem[];
  active: string;
  /**
   * Le diamètre du bouton central — chantier « Accueil et menu bas livreur
   * simplifiés » : 72 points pour le livreur, dont c'est le geste du matin ;
   * 56 par défaut, ce que les autres espaces avaient.
   */
  raisedSize?: number;
}) {
  const insets = useSafeAreaInsets();
  const tokens = useTokens();
  /*
   * Plus le bouton est grand, plus il déborde vers le haut : la moitié de sa
   * hauteur dépasse la barre, et il garde la même assise quelle que soit sa
   * taille.
   */
  const lift = Math.round(raisedSize / 2) - 4;
  /*
   * LA PLACE DU RELIEF EST RÉSERVÉE — chantier « Scanner », point 2. Le bouton
   * débordait SUR l'écran : il recouvrait le pied fixe de chaque écran
   * (« Démarrer ma tournée », « Livrer », « Valider le bon »), et d'autant
   * plus que le téléphone est petit. Une bande au fond de l'écran, de la
   * hauteur du débord, le reçoit désormais : rien ne passe plus dessous. Sur
   * l'écran du bouton lui-même, il est posé à plat et la bande disparaît.
   */
  const raisedShown = items.some((item) => item.raised && item.key !== active);

  return (
    <View style={{ paddingTop: raisedShown ? lift : 0, backgroundColor: tokens.background }}>
      <View
        className="flex-row items-end justify-around border-t border-border bg-card px-1"
        style={{ paddingBottom: Math.max(insets.bottom, 8), paddingTop: 6 }}
      >
        {items.map((item) => {
          const selected = item.key === active;
          const Icon = item.icon;

          const press = () => {
            if (!selected) void Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light);
            item.onPress();
          };

          /*
          SUR SON PROPRE ÉCRAN, LE BOUTON SE POSE — chantier « Scanner »,
          point 2. Le relief déborde de la moitié de son diamètre au-dessus de
          la barre, donc SUR l'écran : sur le scanner, il recouvrait
          « Démarrer ma tournée » et « Livrer ». On y est déjà ; il redevient un
          onglet ordinaire, actif, et ne chevauche plus rien.
        */
          if (item.raised && !selected) {
            return (
              <Pressable
                key={item.key}
                accessibilityRole="button"
                accessibilityLabel={item.label}
                accessibilityState={{ selected }}
                onPress={press}
                className="min-h-[44px] flex-1 items-center"
                hitSlop={{ top: lift }}
                style={({ pressed }) => (pressed ? { transform: [{ scale: 0.95 }] } : null)}
              >
                {/*
                Le bouton central déborde vers le haut : c'est ce qui le
                distingue au premier coup d'œil, et le geste du matin mérite
                d'être trouvé sans lire.
              */}
                <View
                  className="items-center justify-center rounded-full"
                  style={{
                    width: raisedSize,
                    height: raisedSize,
                    marginTop: -lift,
                    backgroundColor: tokens.primary,
                    // L'ombre dit « surélevé » : la couleur est celle du texte,
                    // donc du thème, jamais un noir écrit.
                    shadowColor: tokens.foreground,
                    shadowOpacity: 0.18,
                    shadowRadius: 10,
                    shadowOffset: { width: 0, height: 4 },
                    elevation: 6,
                  }}
                >
                  <Icon size={Math.round(raisedSize * 0.44)} color={tokens['primary-foreground']} />
                </View>
                <Text className="mt-1 text-[11px] text-muted-foreground" numberOfLines={1}>
                  {item.label}
                </Text>
              </Pressable>
            );
          }

          return (
            <Pressable
              key={item.key}
              accessibilityRole="button"
              accessibilityState={{ selected }}
              onPress={press}
              className="min-h-[44px] flex-1 items-center gap-1 pt-1"
            >
              <View>
                <Icon size={22} color={selected ? tokens.primary : tokens['muted-foreground']} />
                {item.badge ? (
                  <View
                    className="absolute -end-2.5 -top-1.5 min-w-[18px] items-center justify-center rounded-full px-1"
                    style={{ backgroundColor: tokens.destructive }}
                  >
                    <Text
                      className="text-[11px] font-bold"
                      style={{ color: tokens['destructive-foreground'] }}
                    >
                      {item.badge > 99 ? '99+' : item.badge}
                    </Text>
                  </View>
                ) : null}
              </View>
              <Text
                className="text-[11px]"
                numberOfLines={1}
                style={{ color: selected ? tokens.primary : tokens['muted-foreground'] }}
              >
                {item.label}
              </Text>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}
