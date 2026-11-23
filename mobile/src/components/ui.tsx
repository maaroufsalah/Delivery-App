/**
 * L'ANCIEN point d'entrée du vocabulaire de base — M1, conservé par M4.
 *
 * Le système de design vit désormais dans `src/ui`. Ce fichier ne contient plus
 * rien : il ré-exporte, pour que les écrans écrits en M1, M2 et M3 continuent
 * d'importer `@/components/ui` sans qu'on les réécrive tous le même jour.
 *
 * Un import de `@/components/ui` dans du code NEUF est une erreur : c'est
 * `@/ui` qui porte le système, et lui seul reçoit les composants ajoutés depuis.
 */
export { Button, Card, EmptyState, Field, iconClass, Skeleton, StatusBadge } from '@/ui';
