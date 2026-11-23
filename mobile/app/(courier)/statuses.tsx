import {
  COURIER_STATUS_FAMILIES,
  courierStatusColor,
  courierStatusMembers,
  type CourierStatusFamily,
} from '@delivery/shared';
import { ChevronRight } from 'lucide-react-native';
import { useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { useI18n, type MessageKey } from '@/i18n';
import { Card, Divider, Screen, ScreenHeader } from '@/ui';

/**
 * LES STATUTS DES COLIS — L2 bloc 7, maquette 14.
 *
 * **Une liste de référence, pas un filtre.** Le livreur vient y lire ce que
 * veut dire un badge qu'il a vu ailleurs. C'est le seul écran de
 * l'application qui n'affiche aucune donnée : il explique le vocabulaire.
 *
 * **Neuf lignes pour trente-quatre statuts.** Anass a raison de regrouper :
 * « deuxième appel + SMS » est un état de plateau, pas de terrain. Le
 * regroupement vit dans `packages/shared` (`COURIER_STATUS_FAMILIES`), avec un
 * test qui vérifie que la partition reste exacte — sans quoi un statut ajouté
 * au référentiel disparaîtrait de cet écran, et un colis prendrait un état que
 * le livreur ne peut lire nulle part.
 *
 * **Chaque ligne se déplie** sur les statuts qu'elle couvre. Ce n'est pas du
 * détail pour le détail : le badge d'une fiche colis porte le libellé PRÉCIS du
 * référentiel — « injoignable », « hors zone » — et cet écran est le seul
 * endroit qui dit à quelle famille il appartient.
 */
export default function CourierStatuses() {
  const { t } = useI18n();

  return (
    <Screen>
      <ScreenHeader title={t('statuses.title')} />

      <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-3 px-5 pb-8">
        <Text className="text-[15px] text-muted-foreground">{t('statuses.hint')}</Text>

        <Card className="gap-1 p-1">
          {COURIER_STATUS_FAMILIES.map((family, index) => (
            <View key={family.key}>
              {index > 0 ? <Divider /> : null}
              <FamilyRow family={family} />
            </View>
          ))}
        </Card>
      </ScrollView>
    </Screen>
  );
}

function FamilyRow({ family }: { family: CourierStatusFamily }) {
  const { t, status } = useI18n();
  const [open, setOpen] = useState(false);

  const members = courierStatusMembers(family);
  const color = courierStatusColor(family);

  return (
    <View>
      <Pressable
        accessibilityRole="button"
        accessibilityState={{ expanded: open }}
        onPress={() => setOpen(!open)}
        className="min-h-[60px] flex-row items-center gap-3 rounded-2xl px-3 py-2"
        style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
      >
        {/* La pastille de la maquette. Sa couleur vient du référentiel, et un
            test garantit qu'elle se voit sur les deux fonds. */}
        <View className="h-3.5 w-3.5 rounded-full" style={{ backgroundColor: color }} />

        <View className="flex-1 gap-0.5">
          <Text className="text-[16px] font-semibold text-foreground">
            {t(`courierStatus.${family.key}` as MessageKey)}
          </Text>
          <Text className="text-[14px] text-muted-foreground">
            {t(`courierStatus.${family.key}.hint` as MessageKey)}
          </Text>
        </View>

        <ChevronRight
          size={18}
          className={open ? 'rotate-90 text-muted-foreground' : 'flip-rtl text-muted-foreground'}
        />
      </Pressable>

      {open ? (
        <View className="gap-1.5 px-3 pb-3 ps-9">
          {members.map((member) => (
            <View key={member.id} className="flex-row items-center gap-2">
              <View className="h-2 w-2 rounded-full" style={{ backgroundColor: member.color }} />
              <Text className="flex-1 text-[14px] text-muted-foreground">{status(member.key)}</Text>
            </View>
          ))}
        </View>
      ) : null}
    </View>
  );
}
