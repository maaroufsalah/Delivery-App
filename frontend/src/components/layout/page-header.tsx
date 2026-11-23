import { BackButton } from '@/components/back-button';
import { cn } from '@/lib/utils';

/**
 * En-tête de page : titre, sous-titre, actions à droite (start/end en RTL).
 *
 * **`backHref` — chantier « Bon de retour livreur », point 4.** Dix-neuf fiches
 * sur vingt-neuf n'offraient AUCUN retour : on y entrait depuis une liste
 * filtrée, et on en sortait par la touche « précédent » du navigateur ou par le
 * menu, qui rouvre la liste au premier écran. Sur une tablette d'agence, la
 * touche du navigateur n'est même pas sous la main.
 *
 * La valeur passée est un **repli**, pas une destination : le bouton recule
 * dans l'historique quand il y a quelque chose derrière, et ne s'en sert que
 * pour un lien ouvert de nulle part. C'est ce qui rend les filtres, la
 * recherche, la page et le tri intacts au retour — ils vivent dans l'URL, donc
 * dans l'entrée d'historique.
 */
export function PageHeader({
  title,
  subtitle,
  actions,
  backHref,
  className,
}: {
  title: string;
  subtitle?: string;
  actions?: React.ReactNode;
  /** La liste dont cette page est un détail. Absent = pas de bouton. */
  backHref?: string;
  className?: string;
}) {
  const header = (
    <div className={cn('dense:pb-4 flex flex-wrap items-start justify-between gap-3 pb-6', className)}>
      <div className="min-w-0">
        <h1 className="text-xl font-semibold tracking-tight sm:text-2xl">{title}</h1>
        {subtitle ? <p className="text-muted-foreground mt-1 text-sm">{subtitle}</p> : null}
      </div>
      {actions ? <div className="flex items-center gap-2">{actions}</div> : null}
    </div>
  );

  if (!backHref) return header;

  return (
    <div className="flex flex-col">
      <BackButton fallbackHref={backHref} className="-ms-2 mb-2 w-fit gap-1.5" />
      {header}
    </div>
  );
}
