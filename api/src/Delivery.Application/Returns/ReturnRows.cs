using System.Globalization;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Returns;

/// <summary>Règles communes de `ReturnsService` : portée, éligibilité, lignes de colis.</summary>
public static class ReturnRows
{
    public const string HandoverPrefix = "RL";
    public const string SlipPrefix = "BRT";
    public static readonly string[] HandoverStatuses = ["draft", "return-requested", "picked-up", "awaiting-receipt", "confirmed", "closed", "cancelled"];
    public static readonly string[] HandoverOrigins = ["admin", "courier"];
    public static readonly string[] Reasons = ["refused", "unreachable", "cancelled", "damaged", "wrong-address", "other"];
    private static readonly ReturnHandoverStatus[] Received = [ReturnHandoverStatus.CONFIRMED, ReturnHandoverStatus.CLOSED];

    public static bool IsEditable(string status) => status is "draft" or "return-requested" or "picked-up";

    public static bool IsCancellable(string status) => status is "draft" or "return-requested" or "picked-up" or "awaiting-receipt";

    /// <summary>`handoverWhere` : administrateur tout, livreur les siens, vendeur rien, agent son agence.</summary>
    public static IQueryable<ReturnHandover> Handovers(IQueryable<ReturnHandover> handovers, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return handovers;
        if (caller.CourierId is { } courierId) return handovers.Where(h => h.CourierId == courierId);
        if (caller.ClientId is not null) return handovers.Where(h => h.Id == Scope.DenyAllId);
        if (caller.AgencyId is { } agencyId) return handovers.Where(h => h.AgencyId == agencyId);
        return handovers.Where(h => h.Id == Scope.DenyAllId);
    }

    /// <summary>`slipWhere` : administrateur tout, vendeur les siens, livreur rien, agent son agence.</summary>
    public static IQueryable<ReturnSlip> Slips(IQueryable<ReturnSlip> slips, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return slips;
        if (caller.ClientId is { } clientId) return slips.Where(s => s.ClientId == clientId);
        if (caller.CourierId is not null) return slips.Where(s => s.Id == Scope.DenyAllId);
        if (caller.AgencyId is { } agencyId) return slips.Where(s => s.AgencyId == agencyId);
        return slips.Where(s => s.Id == Scope.DenyAllId);
    }

    /// <summary>`NOT_RESERVED` : sur aucun bon de retour livreur actif.</summary>
    public static IQueryable<Parcel> NotReserved(IQueryable<Parcel> parcels) => parcels.Where(p => !p.HandoverItems.Any(i => i.ReservedParcelId != null));

    /// <summary>`HANDOVER_ELIGIBLE` : à rapporter par le livreur, sur aucun bon.</summary>
    public static IQueryable<Parcel> HandoverEligible(IQueryable<Parcel> parcels) =>
        NotReserved(parcels.Where(p => StatusCatalog.CourierReturnStatusIds.Contains(p.StatusId)));

    /// <summary>`RECEIVED_AT_AGENCY` : un bon de retour livreur reçu le porte (ligne non corrigée).</summary>
    public static IQueryable<Parcel> ReceivedAtAgency(IQueryable<Parcel> parcels) =>
        parcels.Where(p => p.HandoverItems.Any(i => i.CorrectedAt == null && Received.Contains(i.Handover.Status)));

    /// <summary>`SLIP_ELIGIBLE` : rentré en agence et sur aucun bon de retour client.</summary>
    public static IQueryable<Parcel> SlipEligible(IQueryable<Parcel> parcels) => ReceivedAtAgency(parcels).Where(p => !p.SlipItems.Any());

    public static IQueryable<ReturnParcelLineDto> Lines(IQueryable<Parcel> parcels) => parcels.Select(p => new ReturnParcelLineDto(p.Id, p.Code, p.ExternalCode, p.ClientId, p.Client.Name,
        p.RecipientName, p.RecipientPhone, p.DestinationCity.Name, p.Description, p.StatusId, (long)Math.Round(p.CodAmount * 100m), (long)Math.Round(p.DeliveryFee * 100m),
        p.Courier != null ? p.Courier.FirstName + " " + p.Courier.LastName : null, p.StatusAt, p.Quantity));

    /// <summary>`slipLineFee` : frais d'échec même ville ou inter-villes de la politique de l'agence.</summary>
    public static long SlipLineFee(Domain.Pricing.PricingPolicy policy, string originCityId, string destinationCityId) =>
        originCityId == destinationCityId ? policy.FailureFeeSameCity : policy.FailureFeeCrossCity;

    /// <summary>`assertAgencyReachable` : l'agent n'écrit que dans ses agences.</summary>
    public static async Task AssertAgencyReachableAsync(IAppDbContext db, ICurrentUser caller, string agencyId, CancellationToken cancellationToken)
    {
        if (caller.IsSuperAdmin()) return;
        if (caller.AgencyId is { } own && own != agencyId && !await db.UserAgencies.AnyAsync(l => l.UserId == caller.UserId && l.AgencyId == agencyId, cancellationToken))
        {
            throw BusinessException.Forbidden("Cette agence n'est pas dans votre périmètre.");
        }
    }

    /// <summary>`reachableAgencies` : null pour un administrateur ou un compte sans agence.</summary>
    public static async Task<HashSet<string>?> ReachableAgenciesAsync(IAppDbContext db, ICurrentUser caller, CancellationToken cancellationToken)
    {
        if (caller.IsSuperAdmin() || caller.AgencyId is null) return null;
        var links = await db.UserAgencies.AsNoTracking().Where(l => l.UserId == caller.UserId).Select(l => l.AgencyId).ToListAsync(cancellationToken);
        return [caller.AgencyId, .. links];
    }

    /// <summary>`assertBackOffice`.</summary>
    public static void AssertBackOffice(ICurrentUser caller)
    {
        if (caller.CourierId is not null || caller.ClientId is not null) throw BusinessException.Forbidden("La création en masse des bons de retour est un geste d’agence.");
    }

    public static (DateTime? From, DateTime? To) DayRange(string? from, string? to) =>
        (string.IsNullOrEmpty(from) ? null : Periods.ZonedDayStart(from, ClockDays.Zone), string.IsNullOrEmpty(to) ? null : Periods.ZonedDayStart(Periods.AddDays(to, 1), ClockDays.Zone));

    public static string? Text(string? value) => string.IsNullOrEmpty(value) ? null : value;

    public static ReturnBulkSummaryDto Summarize<T>(IReadOnlyList<(string OwnerId, string AgencyId, List<T> Parcels)> groups, IReadOnlyList<string> codes) =>
        new(groups.Count, groups.Select(g => g.OwnerId).Distinct().Count(), groups.Sum(g => g.Parcels.Count), codes);

    public static async Task<string> ShortNameAsync(IAppDbContext db, CancellationToken cancellationToken) => (await PdfCompany.LoadAsync(db, string.Empty, cancellationToken)).ShortText;
}
