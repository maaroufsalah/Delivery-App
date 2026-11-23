import { useFreshKey } from '@/lib/use-fresh-key';
import { ProfileScreen } from '@/components/profile-screen';

/**
 * PROFIL de l'e-commerçant — M2.
 *
 * Rien de propre au métier à ajouter : un vendeur n'a ni ville d'exercice ni
 * agence de rattachement. Le sous-titre retombe donc sur son adresse e-mail,
 * qui est ce qui identifie son compte chez le transporteur.
 */
export default function MerchantProfileScreen() {
  return <ProfileScreen key={useFreshKey()} />;
}
