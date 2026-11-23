import { useFreshKey } from '@/lib/use-fresh-key';
import { ProfileScreen } from '@/components/profile-screen';
import { useMe } from '@/lib/courier';

/**
 * MES INFORMATIONS — L2 bloc 8, la première ligne de la maquette 12.
 *
 * C'est l'écran que le livreur ouvrait jusqu'ici en touchant « Profil » : son
 * identité, son mot de passe, son verrou biométrique, les pages légales et la
 * suppression de compte. Il ne change pas — il change de PORTE.
 *
 * `settings={false}` : langue, thème et notifications vivent maintenant sous
 * Paramètres (maquette 13). Les laisser aussi ici ferait deux interrupteurs
 * « mode sombre » écrivant la même préférence, et l'un des deux finirait par ne
 * plus la relire. Les espaces marchand et agence, eux, n'ont pas d'écran
 * Paramètres : ils gardent le comportement d'origine.
 */
export default function CourierAccount() {
  const { data: me } = useMe();

  return (
    <ProfileScreen key={useFreshKey()}
      settings={false}
      subtitle={me ? [me.cityName, me.agencyName].filter(Boolean).join(' · ') : null}
    />
  );
}
