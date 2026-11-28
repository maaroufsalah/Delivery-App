using Delivery.Domain.Common;
using Delivery.Infrastructure.Persistence.Seed;

namespace Delivery.UnitTests.Import;

/// <summary>Les montants de Wimo Laravel repris tels quels : `Colis::montantAEncaisser` et la ligne de `FactureResource`.</summary>
public sealed class LaravelFormulasTests
{
    [Theory]
    [InlineData(200, 30, true, false, 200)]   // frais inclus : le prix seul
    [InlineData(200, 30, false, false, 230)]  // frais en sus : prix + frais
    [InlineData(200, 30, false, true, 30)]    // déjà payé, frais en sus : les frais seuls
    [InlineData(200, 30, true, true, 0)]      // déjà payé, frais inclus : rien
    [InlineData(200, 0, false, false, 0)]     // PHP : frais nuls « faux », aucune branche
    public void To_collect_follows_montantAEncaisser(decimal prix, decimal frais, bool inclus, bool dejaPaye, decimal expected) =>
        LaravelFormulas.ToCollect(prix, frais, inclus, dejaPaye).ShouldBe(expected);

    [Fact]
    public void Delivered_fee_included_line_nets_price_minus_fee()
    {
        var line = LaravelFormulas.InvoiceLine(true, 250m, 35m, null, false, true, false);
        line.ShouldBe((250m, 35m, 0m, 215m));
    }

    [Fact]
    public void Delivered_fee_on_top_line_collects_both_and_nets_the_price()
    {
        var line = LaravelFormulas.InvoiceLine(true, 250m, 35m, null, false, false, false);
        line.ShouldBe((285m, 35m, 0m, 250m));
    }

    [Fact]
    public void Already_paid_line_keeps_the_fee_on_top_only()
    {
        LaravelFormulas.InvoiceLine(true, 250m, 35m, null, true, false, false).ShouldBe((35m, 35m, 0m, 0m));
        LaravelFormulas.InvoiceLine(true, 250m, 35m, null, true, true, false).ShouldBe((0m, 35m, 0m, -35m));
    }

    [Fact]
    public void Failure_fee_is_charged_whatever_the_status_and_cancels_the_delivered_amounts()
    {
        LaravelFormulas.InvoiceLine(false, 250m, 35m, 20m, false, true, false).ShouldBe((0m, 0m, 20m, -20m));
        LaravelFormulas.InvoiceLine(true, 250m, 35m, 20m, false, true, false).ShouldBe((0m, 0m, 20m, -20m));
    }

    [Fact]
    public void Returned_without_failure_fee_is_a_zero_line() =>
        LaravelFormulas.InvoiceLine(false, 250m, 35m, null, false, true, false).ShouldBe((0m, 0m, 0m, 0m));

    [Fact]
    public void Sql_null_flags_are_excluded_like_laravel_not_equal_one()
    {
        // `deja_paye != 1` et `frais_inclus != 1` sont inconnus pour NULL : ni le prix ni les frais n'entrent.
        LaravelFormulas.InvoiceLine(true, 250m, 35m, null, null, null, false).ShouldBe((0m, 35m, 0m, -35m));
    }

    [Fact]
    public void Fee_paid_in_cash_is_given_back()
    {
        LaravelFormulas.InvoiceLine(true, 250m, 35m, null, false, true, true).ShouldBe((250m, 0m, 0m, 250m));
    }

    [Fact]
    public void Imported_documents_are_locked_and_live_ones_are_not()
    {
        Should.NotThrow(() => ImportedDocument.EnsureEditable(null, "Ce règlement"));
        var error = Should.Throw<BusinessException>(() => ImportedDocument.EnsureEditable(DateTime.UtcNow, "Ce règlement"));
        error.Message.ShouldContain("repris de Wimo Laravel");
    }
}
