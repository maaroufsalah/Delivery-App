using System.Globalization;
using System.Text.Json;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>Une ville servie par un livreur (`TransferCityChoice`).</summary>
public sealed record TransferCityDto(string Id, string Name);

/// <summary>
/// `OutForDeliveryService` : « En cours de livraison » posé au bureau remet le colis à un livreur, sur son circuit
/// de livraison du jour (en cours dans la ville du colis, sinon le plus récent, sinon le brouillon du jour, sinon un neuf).
/// </summary>
public static class OutForDelivery
{
    public sealed record Leave(string RouteId, string Code, RouteStatus Status, string CourierId);

    public sealed record Reroute(string CityId, string CityName, string FromCityName);

    public sealed record Plan(string ParcelId, string ParcelCode, string CourierId, string CourierName, string RouteId, string RouteCode, string RouteCityId, bool RouteCreated,
        IReadOnlyList<Leave> Leaves, string? PreviousCourierId, Reroute? Reroute, string? ClientId);

    /// <summary>`rerouteCity` : la ville choisie parmi celles du livreur, la seule qu'il sert, sinon il faut choisir.</summary>
    public static TransferCityDto RerouteCity(IReadOnlyList<TransferCityDto> served, string courierName, string? chosenId, int count)
    {
        var cities = served.OrderBy(c => c.Name, StringComparer.Create(CultureInfo.GetCultureInfo("fr"), false)).ToList();
        if (cities.Count == 0)
        {
            throw BusinessException.Conflict($"{courierName} n’a aucune ville cochée : cochez ses villes sur sa fiche pour lui transférer ce colis.", new { reason = "courier-no-city" });
        }
        if (!string.IsNullOrEmpty(chosenId))
        {
            return cities.FirstOrDefault(c => c.Id == chosenId)
                   ?? throw BusinessException.Validation($"Cette ville n’est pas une des villes de {courierName}.", new { reason = "transfer-city-not-served", field = "destinationCityId" });
        }
        if (cities.Count == 1) return cities[0];
        throw BusinessException.Conflict($"{(count > 1 ? $"{count} colis ne vont" : "Ce colis ne va")} dans aucune ville de {courierName} : choisissez la nouvelle destination.",
            new { reason = "transfer-city-required", cities, count });
    }

    public static async Task<Plan> PrepareAsync(IAppDbContext db, ICurrentUser caller, IClock clock, string parcelId, string parcelCode, string destinationCityId, string? parcelCourierId, BillingState billingState,
        string courierId, string? chosenCityId, CancellationToken cancellationToken)
    {
        var courier = await db.Couriers.AsNoTracking().Where(c => c.Id == courierId && c.DeletedAt == null)
                          .Select(c => new { c.Id, c.CityId, c.IsActive, c.FirstName, c.LastName, Cities = c.Cities.Select(r => new TransferCityDto(r.CityId, r.City.Name)).ToList() })
                          .FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound("Livreur introuvable.");
        var courierName = $"{courier.FirstName} {courier.LastName}".Trim();
        if (!courier.IsActive) throw BusinessException.Conflict("Ce livreur est désactivé : il ne reçoit plus de colis.", new { reason = "courier-inactive" });
        if (courier.Cities.Count == 0)
        {
            throw BusinessException.Conflict($"{courierName} n’a aucune ville cochée : complétez sa fiche pour lui confier des colis.", new { reason = "courier-no-city" });
        }
        var served = courier.Cities.Select(c => c.Id).Append(courier.CityId).ToHashSet();
        var wantsReroute = !served.Contains(destinationCityId) || (chosenCityId is not null && chosenCityId != destinationCityId);
        Reroute? reroute = null;
        string? clientId = null;
        if (wantsReroute)
        {
            await ParcelDestination.AssertFreeOfMoneyAsync(db, [(parcelId, parcelCode, billingState)], cancellationToken);
            var city = RerouteCity(courier.Cities, courierName, chosenCityId, 1);
            var current = await db.Parcels.AsNoTracking().Where(p => p.Id == parcelId).Select(p => new { p.ClientId, City = p.DestinationCity.Name }).FirstOrDefaultAsync(cancellationToken);
            clientId = current?.ClientId;
            reroute = new Reroute(city.Id, city.Name, current?.City ?? string.Empty);
        }
        var destination = reroute?.CityId ?? destinationCityId;
        var handover = await db.ReturnHandoverItems.AsNoTracking()
            .Where(i => (i.ParcelId == parcelId || i.ReservedParcelId == parcelId) && i.Handover.Status != ReturnHandoverStatus.CONFIRMED && i.Handover.Status != ReturnHandoverStatus.CLOSED
                        && i.Handover.Status != ReturnHandoverStatus.CANCELLED)
            .Select(i => i.Handover.Code).FirstOrDefaultAsync(cancellationToken);
        if (handover is not null)
        {
            throw BusinessException.Conflict($"Ce colis est sur le bon de retour {handover} : il suit son bon. Annulez d’abord le bon.", new { reason = "parcel-on-handover", handover });
        }
        var links = await db.RouteParcels.AsNoTracking().Where(l => l.ParcelId == parcelId && l.Route.Status != RouteStatus.CLOSED)
            .Select(l => new { l.Route.Id, l.Route.Code, l.Route.Kind, l.Route.Status, l.Route.CourierId }).ToListAsync(cancellationToken);
        if (links.FirstOrDefault(l => l.Kind != RouteKind.DELIVERY) is { } foreign)
        {
            throw BusinessException.Conflict($"Ce colis est sur le circuit {foreign.Code}, qui n’est pas une livraison : retirez-le d’abord de ce circuit.", new { reason = "parcel-on-route", routeCode = foreign.Code });
        }
        // Le brouillon « du jour » : minuit à Casablanca, pas minuit UTC.
        var dayStart = clock.TodayStartUtc();
        var running = await db.Routes.AsNoTracking()
            .Where(r => r.CourierId == courier.Id && r.Kind == RouteKind.DELIVERY && (r.Status == RouteStatus.DISPATCHED || r.Status == RouteStatus.DELIVERING))
            .OrderByDescending(r => r.StatusAt).Take(50).Select(r => new { r.Id, r.Code, r.CityId }).ToListAsync(cancellationToken);
        var draft = await db.Routes.AsNoTracking()
            .Where(r => r.CourierId == courier.Id && r.Kind == RouteKind.DELIVERY && r.Status == RouteStatus.DRAFT && r.CreatedAt >= dayStart)
            .OrderByDescending(r => r.CreatedAt).Select(r => new { r.Id, r.Code, r.CityId }).FirstOrDefaultAsync(cancellationToken);
        var route = running.FirstOrDefault(r => r.CityId == destination) ?? running.FirstOrDefault() ?? draft;
        var created = false;
        if (route is null)
        {
            var opened = await OpenDraftAsync(db, caller, courier.Id, courier.CityId, cancellationToken);
            route = new { opened.Id, opened.Code, opened.CityId };
            created = true;
        }
        return new Plan(parcelId, parcelCode, courier.Id, courierName, route.Id, route.Code, route.CityId, created,
            links.Select(l => new Leave(l.Id, l.Code, l.Status, l.CourierId)).ToList(), parcelCourierId, reroute, clientId);
    }

    /// <summary>Dans la transaction du changement de statut : changement de ville, sortie des autres circuits, entrée sur celui-ci.</summary>
    public static async Task PlaceAsync(IAppDbContext db, ICurrentUser caller, Plan plan, DateTime now, string? authorName, CancellationToken cancellationToken)
    {
        if (plan.Reroute is { } reroute)
        {
            var parcel = await db.Parcels.Include(p => p.DestinationDistrict).FirstAsync(p => p.Id == plan.ParcelId, cancellationToken);
            await ParcelDestination.ApplyAsync(db, caller, parcel, new ChangeParcelDestinationCommand
            {
                Code = plan.ParcelCode, DestinationCityId = reroute.CityId, Reason = $"Remis à {plan.CourierName} (« En cours de livraison » posé au bureau)",
            }, new ParcelDestination.Transfer($"Destination changée : {reroute.FromCityName} → {reroute.CityName} (remis à {plan.CourierName})", 13), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        if (plan.Leaves.Count > 0)
        {
            var ids = plan.Leaves.Select(l => l.RouteId).ToList();
            await db.RouteParcels.Where(l => l.ParcelId == plan.ParcelId && ids.Contains(l.RouteId)).ExecuteDeleteAsync(cancellationToken);
        }
        var last = await db.RouteParcels.Where(l => l.RouteId == plan.RouteId).MaxAsync(l => (int?)l.Sequence, cancellationToken) ?? 0;
        db.RouteParcels.Add(new RouteParcel { RouteId = plan.RouteId, ParcelId = plan.ParcelId, Sequence = last + 1 });
        await db.Parcels.Where(p => p.Id == plan.ParcelId).ExecuteUpdateAsync(s => s.SetProperty(p => p.CourierId, plan.CourierId), cancellationToken);
        var from = string.Join(" / ", plan.Leaves.Select(l => l.Code));
        var summary = $"Remis à {plan.CourierName} sur {plan.RouteCode} (« En cours de livraison » posé au bureau){(from.Length > 0 ? $" — sorti de {from}" : string.Empty)}";
        db.ParcelEvents.Add(new ParcelEvent { ParcelId = plan.ParcelId, StatusId = 13, Comment = summary, AuthorId = caller.UserId, AuthorName = authorName, CityId = plan.RouteCityId, CreatedAt = now.AddMilliseconds(1) });
        db.AuditLogs.Add(new AuditLog
        {
            Entity = "Parcel", EntityId = plan.ParcelId, Action = AuditAction.UPDATE,
            Before = JsonSerializer.Serialize(new { routeCode = from.Length > 0 ? from : null, courierId = plan.PreviousCourierId }),
            After = JsonSerializer.Serialize(new { routeCode = plan.RouteCode, courierId = plan.CourierId, outForDelivery = true, summary }),
            Diff = ["courierId"], UserId = caller.UserId, At = now,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Les suites, hors transaction et sans faire échouer le changement : clôture des circuits quittés, notifications.</summary>
    public static async Task SettleAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, Plan plan, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var leave in plan.Leaves.Where(l => l.Status != RouteStatus.DRAFT && l.RouteId != plan.RouteId))
            {
                await RouteClosing.AutoCloseDeliveryAsync(db, caller, logger, leave.RouteId, cancellationToken);
            }
            await Notifier.NotifyCourierAsync(db, plan.CourierId, "parcel-transferred-in", new Dictionary<string, object> { ["count"] = 1, ["code"] = plan.ParcelCode, ["route"] = plan.RouteCode },
                new Dictionary<string, string> { ["routeCode"] = plan.RouteCode }, cancellationToken);
            if (plan.Reroute is { } reroute && plan.ClientId is not null)
            {
                await Notifier.NotifyMerchantAsync(db, plan.ClientId, "parcel-rerouted", new Dictionary<string, object> { ["code"] = plan.ParcelCode, ["city"] = reroute.CityName },
                    new Dictionary<string, string> { ["parcelCode"] = plan.ParcelCode }, cancellationToken);
                await Notifier.NotifyCourierAsync(db, plan.CourierId, "parcel-destination-changed", new Dictionary<string, object> { ["code"] = plan.ParcelCode },
                    new Dictionary<string, string> { ["topic"] = "parcel-destination-changed", ["parcelCode"] = plan.ParcelCode }, cancellationToken);
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Suites de « En cours de livraison » ({Code}) non jouées : {Message}", plan.ParcelCode, error.Message);
        }
    }

    /// <summary>`createRoute` côté serveur : un brouillon de livraison dans la ville du livreur, sortie aujourd'hui.</summary>
    private static async Task<Route> OpenDraftAsync(IAppDbContext db, ICurrentUser caller, string courierId, string cityId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var origin = await RouteWrites.OriginCityOfAsync(db, caller, cityId, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var route = new Route
        {
            Code = await DocumentCodes.ReserveAsync(db, RouteRows.Prefix, now, cancellationToken), Status = RouteStatus.DRAFT, StatusAt = now, Kind = RouteKind.DELIVERY, CityId = cityId,
            OriginCityId = origin, CourierId = courierId, ExitDate = now,
        };
        db.Routes.Add(route);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return route;
    }
}
