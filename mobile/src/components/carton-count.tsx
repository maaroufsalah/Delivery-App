import { cartonsOf, cartonTotal, showsCartons, type CartonLine } from '@delivery/shared';
import { Text } from 'react-native';
import { useI18n } from '@/i18n';

/**
 * LES CARTONS, SUR LE TÉLÉPHONE — chantier « Cartons sur le mobile »
 * (11/11/2026, priorité de Badr). La suite, côté terrain, de ce que les bons et
 * les circuits disent déjà sur le web et sur leurs PDF.
 *
 * Un colis de Client Pro peut partir en plusieurs cartons sous un seul code
 * (lot G, D-208). Le livreur comptait des lignes et chargeait des cartons :
 *
 *  - `<CartonTag>` : « × 4 cartons » sur la ligne d'un colis qui en porte
 *    plusieurs — et rien sur les autres, comme la fiche du colis le fait déjà.
 *    Une ligne reste un colis ;
 *  - `<CartonTotal>` : « 12 expéditions · 19 cartons » (libellé de Badr, 11/11 :
 *    une ligne est une expédition) en haut d'une liste, seulement
 *    si elle porte au moins un colis de plusieurs cartons (`showsCartons`, la
 *    règle que le web et les PDF lisent aussi).
 *
 * AFFICHAGE SEUL : aucun compteur existant ne change (« X livrés / Y »,
 * « X sur Y traités », « X / Y cochés » comptent des colis).
 */
export function CartonTag({ line }: { line: CartonLine }) {
  const { t } = useI18n();
  const count = cartonsOf(line);
  if (count <= 1) return null;

  return <Text className="text-[13px] font-bold text-foreground">{t('parcel.cartons', { count })}</Text>;
}

export function CartonTotal({
  lines,
  className = '',
}: {
  lines: readonly CartonLine[];
  className?: string;
}) {
  const { t } = useI18n();
  if (!showsCartons(lines)) return null;

  return (
    <Text className={`text-[14px] font-semibold tabular-nums text-muted-foreground ${className}`}>
      {t('cartons.total', { parcels: lines.length, cartons: cartonTotal(lines) })}
    </Text>
  );
}
