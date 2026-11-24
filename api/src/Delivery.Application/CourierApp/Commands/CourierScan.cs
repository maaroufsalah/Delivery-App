using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.CourierApp;

/// <summary>`scanSchema` : le livreur compose sa tournée en scannant ; reprendre le colis d'un autre (`transfer`).</summary>
public sealed record CourierScanCommand : IRequest<object?>
{
    [JsonIgnore]
    public string? IdempotencyKey { get; init; }

    public string? Code { get; init => field = Zod.Trim(value); }
    public string? Kind { get; init; }
    public bool? Transfer { get; init; }
    public string? DestinationCityId { get; init => field = Zod.Trim(value); }
}

public sealed class CourierScanValidator : ZodValidator<CourierScanCommand>
{
    public CourierScanValidator()
    {
        RuleFor(x => x.Code).Required().MinLength(3, "too-short").MaxLength(64, "too-large");
        RuleFor(x => x.Kind).OneOf(["delivery", "pickup"]);
        RuleFor(x => x.DestinationCityId).Must(v => v is null || v.Length >= 1).WithMessage(Zod.TooSmallString(1)).MaxLength(64);
    }
}

internal sealed class CourierScanHandler(IAppDbContext db, ICurrentUser caller, IClock clock, ILogger<CourierScanHandler> logger) : IRequestHandler<CourierScanCommand, object?>
{
    public Task<object?> Handle(CourierScanCommand request, CancellationToken cancellationToken) =>
        IdempotentReplay.RunAsync(db, caller, request.IdempotencyKey, $"POST /courier/me/scan {request.Code}", () => ScanAsync(request, cancellationToken), cancellationToken);

    private sealed record Holder(string Code, RouteStatus Status, RouteKind Kind, string CourierId, string CourierName);

    private async Task<ScanResultDto> ScanAsync(CourierScanCommand request, CancellationToken cancellationToken)
    {
        var courierId = CourierRouteViews.CourierIdOf(caller);
        var code = request.Code!.Trim().ToUpperInvariant();
        var enabled = await db.BoolSettingAsync(SettingKeys.AutoRouteByScan, true, cancellationToken);
        var cityFromCourier = await db.BoolSettingAsync(SettingKeys.ScanCityFromCourier, true, cancellationToken);
        var courier = await db.Couriers.AsNoTracking().Where(c => c.Id == courierId && c.DeletedAt == null).Select(c => new { c.Id, c.CityId, Cities = c.Cities.Select(x => x.CityId).ToList() })
                          .FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound("Fiche livreur introuvable.");
        var parcel = await ParcelLookup.ResolveAsync(db.Parcels.AsNoTracking().Where(p => p.DeletedAt == null), request.Code!, matched => matched
            .Select(p => new { p.Id, p.Code, p.StatusId, p.OriginCityId, Origin = p.OriginCity.Name, p.DestinationCityId, Destination = p.DestinationCity.Name }).FirstOrDefaultAsync(cancellationToken));
        if (parcel is null) throw BusinessException.NotFound($"Colis inconnu : {code}", new { reason = "parcel-unknown", code });
        var kind = request.Kind ?? StatusCatalog.RouteKindForParcelStatus(parcel.StatusId);
        if (kind is null || !StatusCatalog.IsRouteEligible(kind, parcel.StatusId))
        {
            if (!ParcelTransfers.ClosedStatusIds.Contains(parcel.StatusId) && parcel.StatusId != CourierPay.Delivered)
            {
                var held = await HolderAsync(parcel.Id, l => l.Route.CourierId != courierId, cancellationToken);
                if (held is not null)
                {
                    if (request.Transfer == true) return await TakeOverAsync(parcel.Id, courierId, request.DestinationCityId, cancellationToken);
                    throw OtherRoute(code, held, courierId, await RerouteAsync(courier.CityId, courier.Cities, parcel.DestinationCityId, parcel.Destination, held, courierId, cancellationToken));
                }
            }
            throw BusinessException.Conflict($"Le colis {code} n’est pas prêt à partir.", new { reason = "parcel-bad-status", code, statusId = parcel.StatusId });
        }
        var busy = await HolderAsync(parcel.Id, _ => true, cancellationToken);
        if (busy is not null)
        {
            if (busy.CourierId == courierId && busy.Status == RouteStatus.DRAFT) throw BusinessException.Conflict($"Le colis {code} est déjà dans votre tournée.", new { reason = "parcel-already-scanned", code });
            if (request.Transfer == true && busy.CourierId != courierId && busy.Kind == RouteKind.DELIVERY) return await TakeOverAsync(parcel.Id, courierId, request.DestinationCityId, cancellationToken);
            throw OtherRoute(code, busy, courierId, await RerouteAsync(courier.CityId, courier.Cities, parcel.DestinationCityId, parcel.Destination, busy, courierId, cancellationToken));
        }
        var routeKind = RouteWrites.KindOf(kind);
        var dayStart = clock.TodayStartUtc();
        var draft = await db.Routes.AsNoTracking().Where(r => r.CourierId == courierId && r.Kind == routeKind && r.Status == RouteStatus.DRAFT && r.CreatedAt >= dayStart)
            .OrderByDescending(r => r.CreatedAt).Select(r => new { r.Id, r.Code, r.CityId }).FirstOrDefaultAsync(cancellationToken);
        var toSender = kind is "pickup" or "return";
        var parcelCityId = toSender ? parcel.OriginCityId : parcel.DestinationCityId;
        var parcelCityName = toSender ? parcel.Origin : parcel.Destination;
        var routeCityId = draft?.CityId ?? (cityFromCourier ? courier.CityId : parcelCityId);
        if (parcelCityId != routeCityId && !courier.Cities.Contains(parcelCityId))
        {
            throw BusinessException.Conflict($"Le colis {code} est à {parcelCityName}, pas dans cette tournée.", new { reason = "parcel-wrong-city", code, cityName = parcelCityName });
        }
        if (draft is null && !enabled)
        {
            throw BusinessException.Conflict("La création de tournée par scan est désactivée. Demandez à votre agence d’ouvrir la tournée.", new { reason = "scan-disabled" });
        }
        string routeCode;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            string routeId;
            if (draft is null)
            {
                var now = clock.UtcNow;
                var route = new Route
                {
                    Code = await Common.Documents.DocumentCodes.ReserveAsync(db, RouteRows.Prefix, now, cancellationToken), Status = RouteStatus.DRAFT, StatusAt = now, Kind = routeKind,
                    CityId = routeCityId, OriginCityId = routeCityId, CourierId = courierId, ExitDate = now,
                };
                db.Routes.Add(route);
                await db.SaveChangesAsync(cancellationToken);
                (routeId, routeCode) = (route.Id, route.Code);
            }
            else
            {
                (routeId, routeCode) = (draft.Id, draft.Code);
            }
            var last = await db.RouteParcels.Where(l => l.RouteId == routeId).MaxAsync(l => (int?)l.Sequence, cancellationToken) ?? 0;
            db.RouteParcels.Add(new RouteParcel { RouteId = routeId, ParcelId = parcel.Id, Sequence = last + 1 });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        var result = await CourierRouteViews.RouteAsync(db, caller, routeCode, cancellationToken);
        var added = result.Parcels.FirstOrDefault(p => p.Id == parcel.Id) ?? throw BusinessException.Internal("Le colis scanné est introuvable dans la tournée.");
        return new ScanResultDto(result, added, draft is null);
    }

    private Task<Holder?> HolderAsync(string parcelId, System.Linq.Expressions.Expression<Func<RouteParcel, bool>> extra, CancellationToken cancellationToken) =>
        db.RouteParcels.AsNoTracking().Where(l => l.ParcelId == parcelId && l.Route.Status != RouteStatus.CLOSED).Where(extra)
            .Select(l => new Holder(l.Route.Code, l.Route.Status, l.Route.Kind, l.Route.CourierId, (l.Route.Courier.FirstName + " " + l.Route.Courier.LastName).Trim()))
            .FirstOrDefaultAsync(cancellationToken);

    private static BusinessException OtherRoute(string code, Holder route, string courierId, Dictionary<string, object?>? reroute)
    {
        var own = route.CourierId == courierId;
        var details = new Dictionary<string, object?>
        {
            ["reason"] = "parcel-other-route", ["code"] = code, ["courierName"] = route.CourierName, ["routeCode"] = route.Code, ["ownRoute"] = own,
            ["transferable"] = !own && route.Kind == RouteKind.DELIVERY,
        };
        foreach (var (key, value) in reroute ?? []) details[key] = value;
        return BusinessException.Conflict($"Le colis {code} est déjà sur la tournée de {route.CourierName} ({route.Code}).", details);
    }

    /// <summary>`reroute` : ce que la modale de reprise doit proposer quand le colis va hors des villes du livreur.</summary>
    private async Task<Dictionary<string, object?>?> RerouteAsync(string courierCityId, IReadOnlyList<string> grid, string destinationCityId, string destinationName, Holder route, string courierId,
        CancellationToken cancellationToken)
    {
        if (route.Kind != RouteKind.DELIVERY || route.CourierId == courierId) return null;
        var cities = grid.Count == 0 ? [] : await db.Cities.AsNoTracking().Where(c => grid.Contains(c.Id)).Select(c => new TransferCityDto(c.Id, c.Name)).ToListAsync(cancellationToken);
        var served = grid.Append(courierCityId).ToHashSet();
        var toReroute = served.Contains(destinationCityId) ? 0 : 1;
        var options = cities.ToList();
        var keep = served.Contains(destinationCityId) && cities.Count > 0 ? new TransferCityDto(destinationCityId, destinationName) : null;
        if (keep is not null && options.All(c => c.Id != keep.Id)) options.Add(keep);
        var sorted = options.OrderBy(c => c.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("fr"), false)).ToList();
        var covered = toReroute == 0 && cities.Count > 0;
        var result = new Dictionary<string, object?> { ["transferCities"] = sorted, ["destinationCityId"] = destinationCityId, ["destinationCovered"] = covered };
        if (!covered)
        {
            result["destinationCityName"] = destinationName;
            result["rerouteCities"] = sorted;
        }
        return result;
    }

    private async Task<ScanResultDto> TakeOverAsync(string parcelId, string courierId, string? destinationCityId, CancellationToken cancellationToken)
    {
        var enabled = await db.BoolSettingAsync(SettingKeys.AutoRouteByScan, true, cancellationToken);
        var moved = (ParcelTransferResultDto)await ParcelTransfers.RunAsync(db, caller, clock, logger, null,
            new TransferParcelsCommand { ParcelIds = [parcelId], CourierId = courierId, Reason = string.Empty, DestinationCityId = destinationCityId }, new ParcelTransfers.Options(true, enabled),
            cancellationToken);
        if (moved.RouteCode is null) throw BusinessException.Internal("Le colis transféré est introuvable dans la tournée.");
        var result = await CourierRouteViews.RouteAsync(db, caller, moved.RouteCode, cancellationToken);
        var added = result.Parcels.FirstOrDefault(p => p.Id == parcelId) ?? throw BusinessException.Internal("Le colis transféré est introuvable dans la tournée.");
        return new ScanResultDto(result, added, moved.RouteCreated) { Transferred = true, ReroutedTo = moved.ReroutedCityName };
    }
}
