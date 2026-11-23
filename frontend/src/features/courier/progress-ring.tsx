/**
 * L'ANNEAU DE PERFORMANCE — L2 bloc 9, le pendant web de `ProgressRing`.
 *
 * **Le même dessin que le téléphone, par le même moyen** : un arc n'a pas
 * d'équivalent en mise en page, il se trace, et `stroke-dasharray` fait tout le
 * travail — la longueur du trait plein est la fraction de la circonférence.
 * Le tracé part en HAUT et non à droite, où un cercle SVG commence : un anneau
 * qui se remplit depuis trois heures se lit de travers.
 *
 * **Les couleurs viennent des jetons**, `--primary` et `--muted` : le thème du
 * tenant les pose au SSR (`<CompanyStyle />`), et un `#` écrit ici serait la
 * seule couleur de marque en dur de l'application (CLAUDE.md règle 3).
 *
 * **Le pourcentage est du TEXTE HTML** posé par-dessus, pas un `<text>` SVG :
 * il suit alors la taille de police du navigateur, qu'une personne a pu
 * agrandir pour lire.
 */
export function ProgressRing({
  percent,
  size = 96,
  thickness = 10,
  label,
}: {
  percent: number;
  size?: number;
  thickness?: number;
  /** Ce que l'anneau représente, pour qui ne voit pas le dessin. */
  label: string;
}) {
  const bounded = Math.min(100, Math.max(0, percent));
  const radius = (size - thickness) / 2;
  const circumference = 2 * Math.PI * radius;

  return (
    <div
      className="relative shrink-0"
      style={{ width: size, height: size }}
      role="img"
      aria-label={`${label} : ${Math.round(bounded)} %`}
    >
      <svg width={size} height={size} className="-rotate-90" aria-hidden>
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          className="stroke-muted"
          strokeWidth={thickness}
          fill="none"
        />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          className="stroke-primary"
          strokeWidth={thickness}
          strokeLinecap="round"
          fill="none"
          strokeDasharray={`${(circumference * bounded) / 100} ${circumference}`}
        />
      </svg>

      <span className="absolute inset-0 flex items-center justify-center text-lg font-bold">
        {Math.round(bounded)} %
      </span>
    </div>
  );
}
