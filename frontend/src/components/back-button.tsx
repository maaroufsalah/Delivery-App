'use client';

import { ArrowLeft } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import { useRouter } from '@/i18n/navigation';
import { hasInAppHistory } from '@/lib/navigation-depth';

/**
 * LE BOUTON « RETOUR » — chantier « Bon de retour livreur », point 4.
 *
 * **Un seul bouton pour toute l'application**, et il fait la seule chose juste :
 * il RECULE. Les listes portent leurs filtres, leur recherche, leur page et
 * leur tri dans l'URL (`useServerTable`) ; l'entrée d'historique les porte donc
 * aussi, et reculer les rend intacts. Un `<Link href="/parcels">` — ce qu'on
 * avait — rejetait l'agent au premier écran d'une liste de trois mille lignes
 * qu'il venait de filtrer.
 *
 * **Le repli n'est pas un détail.** Un lien collé dans une conversation, un
 * signet, un onglet ouvert d'un clic du milieu : il n'y a alors rien derrière,
 * et `router.back()` ne ferait rien ou sortirait du site. `fallbackHref` dit où
 * aller dans ce cas — la liste dont cette fiche est un élément.
 *
 * **Le retour du NAVIGATEUR marche déjà**, et il marchait déjà avant : c'est la
 * même pile d'historique. Ce bouton n'existe que pour l'offrir au doigt d'un
 * agent sur tablette, qui n'a pas de touche « précédent » sous la main.
 *
 * `flip-rtl` sur la flèche : en arabe, « en arrière » est à droite.
 */
export function BackButton({
  fallbackHref,
  label,
  iconOnly = false,
  className,
}: {
  /** La liste dont cette fiche est un élément — utilisée si l'on arrive de nulle part. */
  fallbackHref: string;
  /** Un libellé plus précis que « Retour » quand la page en mérite un. */
  label?: string;
  /**
   * Sans texte — les en-têtes du livreur, où la largeur se compte en pouces.
   * Le libellé passe en `aria-label` : il disparaît de l'écran, pas du lecteur.
   */
  iconOnly?: boolean;
  className?: string;
}) {
  const t = useTranslations('common');
  const router = useRouter();

  const goBack = () => {
    if (hasInAppHistory()) router.back();
    else router.push(fallbackHref);
  };

  if (iconOnly) {
    return (
      <button
        type="button"
        aria-label={label ?? t('back')}
        onClick={goBack}
        className={
          className ?? 'hover:bg-muted flex size-10 items-center justify-center rounded-lg'
        }
      >
        <ArrowLeft className="flip-rtl size-5" aria-hidden />
      </button>
    );
  }

  return (
    <Button
      variant="ghost"
      size="sm"
      className={className ?? '-ms-2 w-fit gap-1.5'}
      onClick={goBack}
    >
      <ArrowLeft className="flip-rtl size-4" aria-hidden />
      {label ?? t('back')}
    </Button>
  );
}
