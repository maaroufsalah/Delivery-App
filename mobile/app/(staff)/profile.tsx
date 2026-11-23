import { useFreshKey } from '@/lib/use-fresh-key';
import { ProfileScreen } from '@/components/profile-screen';

/**
 * PROFIL du personnel d'agence — M3.
 *
 * Le corps est celui des trois espaces. Rien de propre au métier à ajouter :
 * l'agence de rattachement se lit déjà dans le sélecteur du tableau de bord,
 * là où elle sert à quelque chose.
 */
export default function StaffProfileScreen() {
  return <ProfileScreen key={useFreshKey()} />;
}
