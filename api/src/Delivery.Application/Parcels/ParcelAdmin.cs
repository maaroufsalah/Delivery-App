using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Parcels;

public sealed record ParcelClientChangeField(string Field, string Before, string After);

public sealed record ParcelClientChangeNote(string Code, string Effect);

public sealed record ParcelPartyDto(string Id, string Name);

public sealed record ParcelClientChangePreviewDto(
    string Code, ParcelPartyDto From, ParcelPartyDto To, long FeeBefore, long FeeAfter, long CodAmount,
    IReadOnlyList<ParcelClientChangeField> Changes, IReadOnlyList<ParcelClientChangeNote> Notes, bool BeforePickup, string Fingerprint);

public sealed record ParcelClientChangeResultDto(string Code, string ClientId, long FeeBefore, long FeeAfter, IReadOnlyList<ParcelClientChangeNote> Notes);

/// <summary>`ParcelAdminService` : gardes et plan du changement de client.</summary>
public static class ParcelAdmin
{
    public const string ExchangePrefix = "ECH";
    public const string ReturnPrefix = "RET";

    internal sealed record Plan(ParcelClientChangePreviewDto Preview, Parcel Parcel, Action<Parcel> Apply, IReadOnlyList<(string Id, string Code)> Detached);

    public static void AssertAdmin(ICurrentUser caller)
    {
        if (!caller.IsSuperAdmin()) throw BusinessException.Forbidden("Ce geste est réservé à l’administrateur.", new { reason = "admin-only" });
    }

    public static async Task<Parcel> ParcelAsync(IAppDbContext db, string code, CancellationToken cancellationToken) =>
        await db.Parcels.Include(p => p.Client).FirstOrDefaultAsync(p => p.Code == code && p.DeletedAt == null, cancellationToken)
        ?? throw BusinessException.NotFound($"Colis introuvable : {code}");

    public static async Task AssertNotInOpenRouteAsync(IAppDbContext db, string parcelId, string code, CancellationToken cancellationToken)
    {
        var inRoute = await db.RouteParcels.AsNoTracking().Where(l => l.ParcelId == parcelId && l.Route.Status != RouteStatus.CLOSED)
            .Select(l => l.Route.Code).FirstOrDefaultAsync(cancellationToken);
        if (inRoute is not null)
        {
            throw BusinessException.Validation($"Le colis {code} est sur le circuit {inRoute} : il ne se supprime plus.", new { reason = "parcel-in-route", code, routeCode = inRoute });
        }
    }

    /// <summary>`canCancelExchange`.</summary>
    public static bool CanCancelExchange(ExchangeStatus status) => status is ExchangeStatus.DEMANDE or ExchangeStatus.NOUVEAU_COLIS_CREE or ExchangeStatus.ANCIEN_RECUPERE;

    internal sealed record ExchangeDrop(Exchange? Dossier, Parcel? Return);

    /// <summary>`exchangeOf` : le dossier d'échange et son retour, s'ils peuvent encore tomber.</summary>
    internal static async Task<ExchangeDrop> ExchangeOfAsync(IAppDbContext db, Parcel parcel, CancellationToken cancellationToken)
    {
        var dossier = await db.Exchanges.FirstOrDefaultAsync(x => x.NewParcelId == parcel.Id, cancellationToken);
        if (dossier is not null && dossier.Status != ExchangeStatus.ANNULE && !CanCancelExchange(dossier.Status))
        {
            var wire = Money.ToWire(dossier.Status);
            throw BusinessException.Validation($"L’échange {dossier.Code} est déjà avancé ({wire}) : le colis {parcel.Code} ne se supprime plus.",
                new { reason = "exchange-not-cancellable", code = parcel.Code, exchange = dossier.Code });
        }
        var returnCode = parcel.Code.StartsWith(ExchangePrefix + "-", StringComparison.Ordinal) ? $"{ReturnPrefix}-{parcel.Code[(ExchangePrefix.Length + 1)..]}" : null;
        var ret = returnCode is null ? null : await db.Parcels.FirstOrDefaultAsync(p => p.Code == returnCode && p.Kind == ParcelKind.RETURN && p.DeletedAt == null, cancellationToken);
        if (ret is not null)
        {
            if (ret.StatusId != StatusCatalog.ReturnPending)
            {
                throw BusinessException.Validation($"Le retour {ret.Code} est en « {DocumentLabels.StatusScreen(ret.StatusId)} » : le colis {parcel.Code} ne se supprime plus.",
                    new { reason = "exchange-return-engaged", code = parcel.Code, returnCode = ret.Code });
            }
            var id = ret.Id;
            var on = await db.RouteParcels.Where(l => l.ParcelId == id && l.Route.Status != RouteStatus.CLOSED).Select(l => l.Route.Code).FirstOrDefaultAsync(cancellationToken)
                     ?? await db.ReturnHandoverItems.Where(i => i.ParcelId == id && i.Handover.Status != ReturnHandoverStatus.CANCELLED).Select(i => i.Handover.Code).FirstOrDefaultAsync(cancellationToken)
                     ?? (await db.DeliveryNoteParcels.AnyAsync(l => l.ParcelId == id, cancellationToken)
                         || await db.ReturnNoteParcels.AnyAsync(l => l.ParcelId == id, cancellationToken)
                         || await db.ReturnSlipItems.AnyAsync(i => i.ParcelId == id, cancellationToken) ? "un bon" : null);
            if (on is not null)
            {
                throw BusinessException.Validation($"Le retour {ret.Code} est sur {(on == "un bon" ? on : $"le document {on}")} : le colis {parcel.Code} ne se supprime plus.",
                    new { reason = "exchange-return-engaged", code = parcel.Code, returnCode = ret.Code });
            }
        }
        return new ExchangeDrop(dossier is not null && dossier.Status != ExchangeStatus.ANNULE ? dossier : null, ret);
    }

    internal static void DropExchange(IAppDbContext db, ICurrentUser caller, ExchangeDrop drop, string exchangeParcelCode, string reason, string authorName, DateTime now)
    {
        if (drop.Return is { } ret)
        {
            ret.DeletedAt = now;
            ParcelJournal.Record(db, caller, ret.Id, "deletedAt", null, Iso(now), $"Supprimé avec {exchangeParcelCode} : {reason}", authorName, now);
        }
        if (drop.Dossier is { } dossier)
        {
            dossier.Status = ExchangeStatus.ANNULE;
            dossier.CancelledAt = now;
            db.ExchangeEvents.Add(new ExchangeEvent
            {
                ExchangeId = dossier.Id,
                Status = ExchangeStatus.ANNULE,
                Comment = $"Colis {exchangeParcelCode}{(drop.Return is null ? string.Empty : $" et {drop.Return.Code}")} supprimés : {reason}",
                AuthorId = caller.UserId,
                AuthorName = authorName,
            });
            ParcelJournal.Record(db, caller, dossier.OriginalParcelId, "exchange", dossier.Code, string.Empty, $"Échange annulé, {exchangeParcelCode} supprimé : {reason}", authorName, now);
        }
    }

    /// <summary>`plan` : ce que changerait le passage du colis à un autre client.</summary>
    internal static async Task<Plan> PlanAsync(IAppDbContext db, string code, string clientId, CancellationToken cancellationToken)
    {
        var parcel = await ParcelAsync(db, code, cancellationToken);
        if (parcel.ClientId == clientId) throw BusinessException.Validation("Le colis est déjà sur ce client.", new { reason = "same-client" });
        await AssertMoneyUntouchedAsync(db, parcel, cancellationToken);
        if (parcel.Kind != ParcelKind.STANDARD)
        {
            throw BusinessException.Validation($"{parcel.Code} est un colis de retour ou d’échange : il suit le client de son colis d’origine.", new { reason = "parcel-derived" });
        }
        var derived = await db.Parcels.CountAsync(p => p.ReferenceParcelId == parcel.Id, cancellationToken);
        var claims = await db.Claims.CountAsync(c => c.ParcelId == parcel.Id, cancellationToken);
        var notes = await db.DeliveryNoteParcels.AsNoTracking().Where(l => l.ParcelId == parcel.Id).OrderBy(l => l.DeliveryNote.Code)
            .Select(l => new { l.DeliveryNote.Id, l.DeliveryNote.Code, l.DeliveryNote.Status }).ToListAsync(cancellationToken);
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == clientId && c.DeletedAt == null)
            .Select(c => new
            {
                c.Id, c.Name, c.IsActive, c.IsPro, c.AgencyId, c.Address, c.CityId,
                Pickup = c.PickupAddresses.Where(a => a.IsDefault && a.DeletedAt == null).Select(a => new { a.Address, a.CityId }).FirstOrDefault(),
            }).FirstOrDefaultAsync(cancellationToken);
        if (derived > 0) throw BusinessException.Validation($"{parcel.Code} a déjà un retour ou un échange : son client ne change plus.", new { reason = "parcel-has-derived" });
        if (claims > 0) throw BusinessException.Validation($"{parcel.Code} porte une réclamation de son client : traitez-la avant de changer de client.", new { reason = "parcel-has-claim" });
        if (client is null) throw BusinessException.NotFound("Client introuvable.");
        if (!client.IsActive) throw BusinessException.Validation($"Le client {client.Name} est désactivé.", new { reason = "client-inactive" });
        await ParcelLookup.AssertExternalCodeFreeAsync(db.Parcels, client.Id, parcel.ExternalCode, null, cancellationToken);

        var beforePickup = StatusCatalog.IsEditableBySeller(parcel.StatusId);
        var pickupAddress = client.Pickup?.Address ?? client.Address;
        var pickupCityId = client.Pickup?.CityId ?? client.CityId;
        var originCityId = beforePickup ? pickupCityId : parcel.OriginCityId;
        var nextPickupAddress = beforePickup ? pickupAddress ?? parcel.PickupAddress : parcel.PickupAddress;
        var agencyId = beforePickup ? client.AgencyId ?? parcel.AgencyId : parcel.AgencyId;
        var quote = await ParcelPricing.QuoteAsync(db, originCityId, parcel.DestinationCityId, Money.ToWire(parcel.DeliveryMode), client.Id, cancellationToken);
        var feeBefore = Money.ToCents(parcel.DeliveryFee);
        var feeAfter = ParcelInputs.CartonsFee(quote.Fee, parcel.Quantity);
        ExpectedPayment? expectedPayment = client.IsPro ? parcel.ExpectedPayment ?? ExpectedPayment.CASH : null;
        var blRequested = client.IsPro && parcel.BlRequested;

        var changes = new List<ParcelClientChangeField> { new("clientId", parcel.ClientId, client.Id) };
        void Push(string field, string before, string after)
        {
            if (before != after) changes.Add(new ParcelClientChangeField(field, before, after));
        }
        Push("deliveryFee", feeBefore.ToString(CultureInfo.InvariantCulture), feeAfter.ToString(CultureInfo.InvariantCulture));
        Push("pickupAddress", parcel.PickupAddress, nextPickupAddress);
        Push("originCityId", parcel.OriginCityId, originCityId);
        Push("agencyId", parcel.AgencyId, agencyId);
        Push("expectedPayment", parcel.ExpectedPayment?.ToString() ?? string.Empty, expectedPayment?.ToString() ?? string.Empty);
        Push("blRequested", parcel.BlRequested ? "true" : "false", blRequested ? "true" : "false");

        var noteEffects = notes.Select(n => new ParcelClientChangeNote(n.Code, n.Status == DeliveryNoteStatus.VALIDATED ? "kept" : "detached")).ToList();
        var detached = notes.Where(n => n.Status != DeliveryNoteStatus.VALIDATED).Select(n => (n.Id, n.Code)).ToList();
        var fingerprintSource = JsonSerializer.Serialize(new object[] { parcel.Id, parcel.StatusId, parcel.ClientId, feeBefore, changes, noteEffects }, JsonSerializerOptions.Web);
        var fingerprint = Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(fingerprintSource)))[..16];

        void Apply(Parcel target)
        {
            target.ClientId = client.Id;
            target.DeliveryFee = Money.FromCents(feeAfter);
            target.PickupAddress = nextPickupAddress;
            target.OriginCityId = originCityId;
            target.AgencyId = agencyId;
            target.ExpectedPayment = expectedPayment;
            target.BlRequested = blRequested;
        }
        var preview = new ParcelClientChangePreviewDto(parcel.Code, new ParcelPartyDto(parcel.Client.Id, parcel.Client.Name), new ParcelPartyDto(client.Id, client.Name),
            feeBefore, feeAfter, Money.ToCents(parcel.CodAmount), changes, noteEffects, beforePickup, fingerprint);
        return new Plan(preview, parcel, Apply, detached);
    }

    private static async Task AssertMoneyUntouchedAsync(IAppDbContext db, Parcel parcel, CancellationToken cancellationToken)
    {
        var invoiced = await db.InvoiceLines.AsNoTracking().Where(l => l.ParcelId == parcel.Id).Select(l => l.Invoice.Code).FirstOrDefaultAsync(cancellationToken);
        var payout = await db.CodPayoutParcels.AsNoTracking().Where(p => p.ParcelId == parcel.Id).Select(p => p.Payout.Code).FirstOrDefaultAsync(cancellationToken);
        var transfer = await db.TransferOrderParcels.AsNoTracking().Where(t => t.ParcelId == parcel.Id).Select(t => t.TransferOrder.Code).FirstOrDefaultAsync(cancellationToken);
        if (parcel.BillingState == BillingState.INVOICED || parcel.FinancialStatus == FinancialStatus.FACTURE || invoiced is not null)
        {
            throw BusinessException.Validation($"{parcel.Code} est facturé{(invoiced is null ? string.Empty : $" ({invoiced})")} : il ne change plus de client.",
                new { reason = "parcel-invoiced", invoiceCode = invoiced });
        }
        if (payout is not null)
        {
            throw BusinessException.Validation($"{parcel.Code} est sur le bon de versement {payout} : il ne change plus de client.", new { reason = "parcel-on-payout", payoutCode = payout });
        }
        if (transfer is not null)
        {
            throw BusinessException.Validation($"{parcel.Code} est dans l’ordre de virement {transfer} : il ne change plus de client.", new { reason = "parcel-transferred", transferOrderCode = transfer });
        }
        if (parcel.ReimbursedByCourier || parcel.FinancialStatus == FinancialStatus.REMBOURSE || parcel.BillingState == BillingState.REIMBURSED)
        {
            throw BusinessException.Validation($"{parcel.Code} est déjà remboursé au client : il ne change plus de client.", new { reason = "parcel-reimbursed" });
        }
        if (parcel.FinancialStatus != FinancialStatus.NON_ENCAISSE)
        {
            throw BusinessException.Validation($"{parcel.Code} est déjà encaissé : il ne change plus de client.", new { reason = "parcel-collected" });
        }
    }

    public static string Iso(DateTime value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
