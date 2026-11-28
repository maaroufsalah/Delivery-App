using System.Net;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.ContractTests.Referential;

/// <summary>
/// Reprise de l'opérationnel et de l'argent de Wimo Laravel sur un jeu synthétique : codes d'origine, statut 36 tranché,
/// montants de facture et de recette par les formules Laravel, documents clos verrouillés, documents ouverts vivants,
/// essai à blanc sans écriture, relance identique (`legacy_mappings`).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class LaravelOperationsImportContractTests(ApiFactory factory)
{
    private const string N = "\\N";

    private static string Csv(params string[][] rows) => string.Join('\n', rows.Select(r => string.Join(',', r.Select(c => c.Contains(',') ? $"\"{c}\"" : c)))) + "\n";

    private static readonly string[] ColisHeader =
    [
        "id", "ramassage_ville_id", "livraison_ville_id", "designation", "prix", "destinataire", "tel_destinataire", "ramassage_adresse", "livraison_adresse", "ouverture_colis",
        "colis_fragile", "created_at", "updated_at", "livraison_id", "livraison_type", "ramassage_id", "ramassage_type", "statut_id", "code", "client_id", "frais", "deja_paye",
        "whstatut_id", "sys_agence", "frais_inclus", "livreur_id", "user_id", "statut_date", "date_report", "frais_deja_paye", "latitude", "longitude", "emplacement_id",
        "emplacement_type", "frais_echoue", "reference_id", "retour", "ramassage_exonere", "colis_echoue", "deleted_at", "code_externe", "type_livraison_id", "etat_id",
    ];

    private static string[] Colis(string id, string code, string status, string prix, string frais, string inclus, string? echoue = null, string? reference = null,
        string? deleted = null) =>
    [
        id, "1", "1", "Article", prix, "Client final", "0611111111", "Dépôt", "Rue 2", "0", "0", "2026-10-01 10:00:00", "2026-10-03 10:00:00", "1", "App\\Quartier", "1",
        "App\\Agence", status, code, "5", frais, "0", N, "1", inclus, "8", "12", "2026-10-02 12:00:00", N, N, N, N, "1", "App\\Ville", echoue ?? N, reference ?? N, N, N,
        echoue is null ? N : "1", deleted ?? N, N, "1", N,
    ];

    private async Task<LaravelImporter.Report> ImportAsync(string folder, bool apply)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<LaravelImporter>().ImportAsync(folder, new LaravelImporter.ImportOptions(apply, BatchSize: 2));
    }

    [Fact]
    public async Task Imports_parcels_history_documents_and_money_with_laravel_amounts_then_reruns_unchanged()
    {
        // Les identifiants Laravel du jeu sont ceux du test du référentiel : chaque test part d'une correspondance vide.
        await factory.ExecuteDbAsync(db => db.LegacyMappings.ExecuteDeleteAsync());
        var (folder, tag, codes) = await LaravelImportContractTests.FixtureFor(factory);
        void W(string table, params string[][] rows) => File.WriteAllText(Path.Combine(folder, $"{table}.csv"), Csv(rows));
        // Livraison en ville 1 = codes[0] (Alpha, grille du livreur : 15) ; le code dit Alpha → Beta (compteur).
        var code = $"{codes[0]}01{Random.Shared.Next(100, 999)}2610{codes[1]}";
        W("colis", ColisHeader,
            Colis("100", code, "4", "250", "35", "1"),
            Colis("101", $"{code[..5]}9{code[6..]}", "6", "100", "35", "1", echoue: "20"),
            Colis("102", $"{code[..5]}8{code[6..]}", "36", "120", "35", "0"),
            Colis("103", $"{code[..5]}7{code[6..]}", "4", "90", "35", "1", deleted: "2026-10-05 10:00:00"),
            Colis("104", $"ECH-{code}", "4", "50", "35", "1", reference: "100"));
        W("colis_trakings", ["id", "colis_id", "user_id", "statut_id", "commentaire", "file", "date_report", "created_at", "updated_at", "confirmation", "confirmation_user",
                "confirmation_date", "frais_livreur"],
            ["1", "100", "13", "3", N, N, N, "2026-10-01 11:00:00", N, N, N, N, N],
            ["2", "100", "10", N, "Appel client", N, N, "2026-10-01 12:00:00", N, N, N, N, N],
            ["3", "100", "13", "4", N, N, N, "2026-10-02 12:00:00", N, "1", "10", "2026-10-02 18:00:00", "15"],
            ["4", "102", "13", "36", N, N, N, "2026-10-02 13:00:00", N, N, N, N, N]);
        W("bon_livraisons", ["id", "code", "client_id", "created_at", "updated_at", "statut_id", "sys_agence", "type", "emplacement_id", "emplacement_type"],
            ["90", $"BL{tag}", "5", "2026-10-01 09:00:00", "2026-10-01 15:00:00", "2", "1", "ramassage", "1", "App\\Ville"]);
        W("bon_livraison_colis", ["id", "bon_livraison_id", "colis_id"], ["1", "90", "100"], ["2", "90", "101"]);
        W("boxes", ["id", "code", "ville_id", "statut_id", "partenaire_id", "destinataire_id", "destinataire_type", "created_at", "updated_at", "sys_agence", "type", "date_sorti",
                "statut_date", "depart_id", "ramassage", "type_circuit"],
            ["80", $"CA{tag}", "2", "6", N, "8", "App\\Livreur", "2026-10-02 08:00:00", N, "1", "circuit", "2026-10-02", N, "1", N, "livraison"],
            ["81", $"CB{tag}", "2", "6", N, "8", "App\\Livreur", "2026-10-02 08:00:00", N, "1", "circuit", "2026-10-02", N, "1", N, "livraison"]);
        W("boxe_colis", ["id", "boxe_id", "colis_id", "confirmed", "confirmed_user", "confirmed_date"],
            ["1", "80", "100", "1", "10", "2026-10-02 18:00:00"], ["2", "80", "102", N, N, N], ["3", "81", "101", "1", "10", "2026-10-02 18:00:00"]);
        W("factures", ["id", "code", "client_id", "statut_id", "created_at", "updated_at", "sys_agence", "livreur_id"],
            ["50", $"FC{tag}", "5", "3", "2026-10-10 10:00:00", "2026-10-20 10:00:00", "1", N],
            ["51", $"FL{tag}", N, "2", "2026-10-10 10:00:00", "2026-10-11 10:00:00", "1", "8"]);
        W("facture_colis", ["id", "facture_id", "colis_id"], ["1", "50", "100"], ["2", "50", "101"], ["3", "50", "103"]);
        W("facture_livreur_colis", ["id", "facture_id", "traking_id"], ["1", "51", "3"]);
        W("rembourssements", ["id", "client_id", "mode_id", "compte_id", "facture_id", "montant", "commentaire", "reference", "file", "date_paiement", "created_at", "updated_at",
                "sys_agence", "livreur_id"],
            ["60", "5", "2", "1", "50", "195", N, "VIR-1", N, "2026-10-20", "2026-10-20 10:00:00", N, "1", N]);
        W("recettes", ["id", "code", "ville_id", "confirmation", "confirmation_user", "confirmation_date", "partenaire_id", "partenaire_type", "file", "commentaire", "created_at",
                "updated_at"],
            ["70", $"RA{tag}", "2", "1", "10", "2026-10-02 19:00:00", "8", "App\\Livreur", N, N, "2026-10-02 18:30:00", N],
            ["71", $"RB{tag}", "2", N, N, N, "8", "App\\Livreur", N, N, "2026-10-03 18:30:00", N]);
        W("recette_colis", ["id", "recette_id", "colis_id", "confirmation"], ["1", "70", "100", "1"], ["2", "70", "101", "1"], ["3", "71", "104", N]);

        var dry = await ImportAsync(folder, apply: false);
        dry.Tables.Single(t => t.Table == "colis").Imported.ShouldBe(4);
        (await factory.QueryDbAsync(db => db.Parcels.CountAsync(p => p.Code == code))).ShouldBe(0);
        (await factory.QueryDbAsync(db => db.LegacyMappings.CountAsync())).ShouldBe(0);

        var report = await ImportAsync(folder, apply: true);
        report.Tables.Single(t => t.Table == "colis").Ignored.Keys.ShouldContain("colis supprimé dans Laravel");

        var parcels = await factory.QueryDbAsync(db => db.Parcels.Where(p => p.Code.EndsWith(code[6..]) || p.Code == $"ECH-{code}")
            .Select(p => new { p.Id, p.Code, p.StatusId, p.BillingState, p.FinancialStatus, p.ReimbursedByCourier, p.ReimbursedRemittanceId, p.CourierCommission, p.Kind,
                Reference = p.ReferenceParcel!.Code, Events = p.Events.OrderBy(e => e.CreatedAt).Select(e => e.StatusId).ToList(), p.CreatedAt }).ToListAsync());
        parcels.Count.ShouldBe(4);
        var delivered = parcels.Single(p => p.Code == code);
        delivered.StatusId.ShouldBe(4);
        delivered.BillingState.ShouldBe(BillingState.REIMBURSED);
        delivered.FinancialStatus.ShouldBe(FinancialStatus.REMBOURSE);
        delivered.ReimbursedByCourier.ShouldBeTrue();
        delivered.CourierCommission.ShouldBe(15m);
        delivered.Events.ShouldBe([3, 3, 4]);
        delivered.CreatedAt.ShouldBe(new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc));
        parcels.Single(p => p.Code.StartsWith($"{code[..5]}8")).StatusId.ShouldBe(33);
        parcels.Single(p => p.Code.StartsWith($"{code[..5]}8")).BillingState.ShouldBe(BillingState.TO_INVOICE);
        var exchange = parcels.Single(p => p.Code == $"ECH-{code}");
        exchange.Kind.ShouldBe(ParcelKind.EXCHANGE);
        exchange.Reference.ShouldBe(code);

        // Facture : colisLivres 250 − frais 35 − frais d'échec 20 = 195 ; la ligne du colis supprimé ne compte pas.
        var invoice = await factory.QueryDbAsync(db => db.Invoices.Where(i => i.Code == $"FC{tag}")
            .Select(i => new { i.Status, i.TotalCod, i.TotalDeliveryFee, i.TotalFailureFee, i.TotalDue, i.ImportedAt, Lines = i.Lines.Count(l => l.InvoicedParcelId != null),
                Paid = i.Payments.Sum(p => p.Amount) }).SingleAsync());
        invoice.Status.ShouldBe(InvoiceStatus.PAID);
        (invoice.TotalCod, invoice.TotalDeliveryFee, invoice.TotalFailureFee, invoice.TotalDue).ShouldBe((250m, 35m, 20m, 195m));
        invoice.ImportedAt.ShouldNotBeNull();
        invoice.Lines.ShouldBe(2);
        invoice.Paid.ShouldBe(195m);

        // Recette acceptée : COD 250 (frais inclus), frais du livreur 15 (grille ville 1), net 235 ; le brouillon reste ouvert.
        var remittances = await factory.QueryDbAsync(db => db.CashRemittances.Where(r => r.Code == $"RA{tag}" || r.Code == $"RB{tag}")
            .Select(r => new { r.Id, r.Code, r.Status, r.ExpectedCod, r.ExpectedCommission, r.ExpectedNet, r.ImportedAt }).ToListAsync());
        var accepted = remittances.Single(r => r.Code == $"RA{tag}");
        accepted.Status.ShouldBe(CashRemittanceStatus.CONFIRMED);
        (accepted.ExpectedCod, accepted.ExpectedCommission, accepted.ExpectedNet).ShouldBe((250m, 15m, 235m));
        accepted.ImportedAt.ShouldNotBeNull();
        delivered.ReimbursedRemittanceId.ShouldBe(accepted.Id);
        var open = remittances.Single(r => r.Code == $"RB{tag}");
        open.Status.ShouldBe(CashRemittanceStatus.DRAFT);
        open.ImportedAt.ShouldBeNull();

        var routes = await factory.QueryDbAsync(db => db.Routes.Where(r => r.Code == $"CA{tag}" || r.Code == $"CB{tag}")
            .Select(r => new { r.Code, r.Status, r.ImportedAt, Parcels = r.Parcels.Count }).ToListAsync());
        routes.Single(r => r.Code == $"CA{tag}").Status.ShouldBe(RouteStatus.DISPATCHED);
        routes.Single(r => r.Code == $"CA{tag}").ImportedAt.ShouldBeNull();
        routes.Single(r => r.Code == $"CB{tag}").Status.ShouldBe(RouteStatus.CLOSED);
        routes.Single(r => r.Code == $"CB{tag}").ImportedAt.ShouldNotBeNull();
        (await factory.QueryDbAsync(db => db.DeliveryNotes.Where(n => n.Code == $"BL{tag}").Select(n => new { n.Type, n.Status, Parcels = n.Parcels.Count }).SingleAsync()))
            .ShouldBe(new { Type = DeliveryNoteType.PICKUP, Status = DeliveryNoteStatus.VALIDATED, Parcels = 2 });
        (await factory.QueryDbAsync(db => db.CourierStatements.Where(s => s.Code == $"FL{tag}").Select(s => new { s.Status, s.TotalDue }).SingleAsync()))
            .ShouldBe(new { Status = CourierStatementStatus.VALIDATED, TotalDue = 15m });
        (await factory.QueryDbAsync(db => db.Exchanges.CountAsync(e => e.NewParcel!.Code == $"ECH-{code}"))).ShouldBe(1);

        // Relance : tout l'opérationnel est « identique », rien ne double.
        var rerun = await ImportAsync(folder, apply: true);
        foreach (var table in new[] { "colis", "colis_trakings", "bon_livraisons", "boxes", "factures (clients)", "rembourssements", "factures (livreurs)", "recettes" })
        {
            var t = rerun.Tables.Single(x => x.Table == table);
            (t.Imported, t.Updated).ShouldBe((0, 0), table);
            t.Unchanged.ShouldBeGreaterThan(0, table);
        }
        (await factory.QueryDbAsync(db => db.Parcels.CountAsync(p => p.Code == code))).ShouldBe(1);

        // Un règlement et une caisse repris ne se suppriment pas.
        var admin = await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.SUPER_ADMIN));
        var paymentId = await factory.QueryDbAsync(db => db.Payments.Where(p => p.Invoice!.Code == $"FC{tag}").Select(p => p.Id).SingleAsync());
        var refused = await admin.DeleteAsync($"/api/payments/{paymentId}");
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe(ImportedDocument.Reason);
        (await admin.DeleteAsync($"/api/remittances/RA{tag}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        Directory.Delete(folder, true);
    }
}
