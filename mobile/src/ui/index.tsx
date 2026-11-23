/**
 * LE SYSTÈME DE DESIGN de l'application — M4 bloc 1.
 *
 * Un seul point d'entrée : `import { Card, Chip, KpiTile } from '@/ui'`. Les
 * écrans ne connaissent jamais le fichier où vit un composant, ce qui permet
 * d'en déplacer un sans toucher aux onze écrans qui l'appellent.
 *
 * **Trois règles tenues ici plutôt que répétées partout** :
 *
 *  1. **44 points de haut minimum** sur tout ce qui se touche. C'est la cible
 *     recommandée, et la différence entre « ça marche » et « ça marche avec des
 *     gants », debout, sur une moto à l'arrêt.
 *  2. **16 points de texte minimum** pour le corps. En dessous, un écran de
 *     téléphone en plein soleil marocain ne se lit plus.
 *  3. **Aucune couleur écrite.** Tout vient des jetons du tenant — par une
 *     classe (`bg-primary`) quand la propriété l'accepte, par `useTokens()`
 *     quand elle ne l'accepte pas (dégradés, SVG, barres système). Une valeur
 *     littérale dans ce dossier est un défaut, pas un raccourci.
 */

export { iconClass, Button, Field } from './controls';
export {
  Card,
  Chip,
  KpiTile,
  KpiRow,
  ProgressBar,
  ProgressRing,
  StatusBadge,
  Divider,
} from './surfaces';
export { Skeleton, SkeletonCard, EmptyState } from './states';
export { Screen, GradientHeader, ScreenHeader, SectionTitle } from './layout';
export { Sheet, SheetItem } from './sheet';
export { BottomBar, type BottomBarItem } from './bottom-bar';
export { Timeline, type TimelineEntry } from './timeline';
export { FadeIn } from './motion';
export { Toast, useToast, ToastProvider } from './toast';
