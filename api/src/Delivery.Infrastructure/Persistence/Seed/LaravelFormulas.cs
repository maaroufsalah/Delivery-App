namespace Delivery.Infrastructure.Persistence.Seed;

/// <summary>Les montants tels que Wimo Laravel les calcule à l'affichage, appliqués une fois à la reprise.</summary>
internal static class LaravelFormulas
{
    /// <summary>`Colis::montantAEncaisser` : ce que le livreur encaisse (PHP : un prix ou des frais nuls sont « faux »).</summary>
    public static decimal ToCollect(decimal prix, decimal frais, bool fraisInclus, bool dejaPaye)
    {
        if (fraisInclus) return prix != 0 && !dejaPaye ? prix : 0m;
        if (frais != 0) return prix != 0 && !dejaPaye ? prix + frais : frais;
        return 0m;
    }

    /// <summary>
    /// Une ligne de facture client (`App\Http\Resources\Facture`) : `colisLivres` (+prix si `deja_paye != 1`),
    /// `colisLivresFraisNonInclu` (+frais si `frais_inclus != 1`), `colisSumFrais` (−frais), `colisSumFraisEchoue`
    /// (−frais d'échec), `colisSumFraisPayeEspece` (+frais payés en espèces). Les filtres SQL `!= 1` excluent NULL.
    /// </summary>
    public static (decimal Cod, decimal Fee, decimal Failure, decimal Net) InvoiceLine(bool delivered, decimal prix, decimal frais, decimal? fraisEchoue,
        bool? dejaPaye, bool? fraisInclus, bool fraisDejaPaye)
    {
        decimal cod = 0m, fee = 0m, failure = 0m, cash = 0m;
        if (delivered && fraisEchoue is null)
        {
            if (dejaPaye == false) cod += prix;
            if (fraisInclus == false) cod += frais;
            fee = frais;
        }
        if (fraisEchoue is not null) failure = fraisEchoue.Value;
        if (delivered && fraisDejaPaye) cash = frais;
        return (cod, fee - cash, failure, cod - fee - failure + cash);
    }
}
