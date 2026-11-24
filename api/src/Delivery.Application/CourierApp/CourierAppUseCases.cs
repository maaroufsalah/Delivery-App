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

/// <summary>`GET /courier/me/routes` (`courierRoutesQuerySchema`) : brouillon ou tournées parties ; l'historique (`closed`) rend une liste vide.</summary>
public sealed record GetCourierRoutesQuery(string? Status, string? From, string? Kind) : IRequest<IReadOnlyList<CourierRouteDto>>;

public sealed class GetCourierRoutesValidator : ZodValidator<GetCourierRoutesQuery>
{
    public GetCourierRoutesValidator()
    {
        RuleFor(x => x.Status).OneOf(["draft", "active", "closed"]);
        RuleFor(x => x.Kind).OneOf(["return"]);
    }
}

internal sealed class GetCourierRoutesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCourierRoutesQuery, IReadOnlyList<CourierRouteDto>>
{
    public async Task<IReadOnlyList<CourierRouteDto>> Handle(GetCourierRoutesQuery request, CancellationToken cancellationToken)
    {
        var courierId = CourierRouteViews.CourierIdOf(caller);
        var status = request.Status ?? "active";
        if (status == "closed") return [];
        var routes = db.Routes.AsNoTracking().Where(r => r.CourierId == courierId && r.Status == (status == "draft" ? RouteStatus.DRAFT : RouteStatus.DISPATCHED));
        routes = request.Kind == "return" ? routes.Where(r => r.Kind == RouteKind.RETURN) : routes.Where(r => r.Kind == RouteKind.DELIVERY || r.Kind == RouteKind.PICKUP);
        var from = request.From?.Trim();
        if (!string.IsNullOrEmpty(from))
        {
            // `dayRange` : à partir de minuit à Casablanca, pas de minuit UTC.
            var start = Periods.ZonedDayStart(from, ClockDays.Zone);
            routes = routes.Where(r => r.CreatedAt >= start);
        }
        var rows = await CourierRouteViews.LoadAsync(db, routes.OrderByDescending(r => r.CreatedAt).Take(20), cancellationToken);
        return rows.Select(CourierRouteViews.ToRoute).ToList();
    }
}

/// <summary>`GET /courier/me/deliveries` : le menu Livraison, toutes tournées de livraison parties confondues.</summary>
public sealed record GetCourierDeliveriesQuery : IRequest<CourierDeliveriesDto>;

internal sealed class GetCourierDeliveriesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCourierDeliveriesQuery, CourierDeliveriesDto>
{
    public async Task<CourierDeliveriesDto> Handle(GetCourierDeliveriesQuery request, CancellationToken cancellationToken)
    {
        var courierId = CourierRouteViews.CourierIdOf(caller);
        var rows = await CourierRouteViews.LoadAsync(db, db.Routes.AsNoTracking()
            .Where(r => r.CourierId == courierId && r.Kind == RouteKind.DELIVERY && (r.Status == RouteStatus.DISPATCHED || r.Status == RouteStatus.DELIVERING))
            .OrderByDescending(r => r.CreatedAt).Take(20), cancellationToken);
        var seen = new HashSet<string>();
        var parcels = new List<CourierDeliveryParcelDto>();
        var delivered = 0;
        foreach (var route in rows)
        {
            foreach (var link in route.Parcels)
            {
                if (!seen.Add(link.View.Id)) continue;
                if (link.View.StatusId == CourierPay.Delivered)
                {
                    delivered++;
                    continue;
                }
                if (!StatusCatalog.IsCourierDeliveryPending(link.View.StatusId)) continue;
                if (link.Handovers.Any(h => h.ReservedParcelId is not null && h.Status == ReturnHandoverStatus.AWAITING_RECEIPT)) continue;
                parcels.Add(Copy(link.View, route.Code));
            }
        }
        return new CourierDeliveriesDto(delivered, seen.Count, parcels);
    }

    private static CourierDeliveryParcelDto Copy(CourierRouteParcelDto p, string routeCode) => new()
    {
        Id = p.Id, Code = p.Code, ExternalCode = p.ExternalCode, Sequence = p.Sequence, RecipientName = p.RecipientName, RecipientPhone = p.RecipientPhone, DeliveryAddress = p.DeliveryAddress,
        DestinationCityName = p.DestinationCityName, DestinationDistrictName = p.DestinationDistrictName, SenderName = p.SenderName, SenderPhone = p.SenderPhone, PickupAddress = p.PickupAddress,
        OriginCityName = p.OriginCityName, StatusId = p.StatusId, CodToCollect = p.CodToCollect, Quantity = p.Quantity, CourierCommission = p.CourierCommission, Confirmed = p.Confirmed,
        Latitude = p.Latitude, Longitude = p.Longitude, RouteCode = routeCode,
    };
}

/// <summary>`GET /courier/me/routes/:code`.</summary>
public sealed record GetCourierRouteQuery(string Code) : IRequest<CourierRouteDto>;

internal sealed class GetCourierRouteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCourierRouteQuery, CourierRouteDto>
{
    public Task<CourierRouteDto> Handle(GetCourierRouteQuery request, CancellationToken cancellationToken) => CourierRouteViews.RouteAsync(db, caller, request.Code, cancellationToken);
}

/// <summary>Où est ce colis pour ce livreur (`locate`) : sur une de ses tournées ouvertes, la plus récente.</summary>
public static class CourierParcels
{
    public sealed record Located(string ParcelId, string Code, int StatusId, string RouteCode, RouteKind RouteKind);

    public static async Task<Located> LocateAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var courierId = CourierRouteViews.CourierIdOf(caller);
        var parcels = db.Parcels.AsNoTracking().Where(p => p.DeletedAt == null);
        var link = await ParcelLookup.ResolveAsync(parcels, code, async matched =>
        {
            var ids = matched.Select(p => p.Id);
            return await db.RouteParcels.AsNoTracking().Where(l => ids.Contains(l.ParcelId) && l.Route.CourierId == courierId && l.Route.Status != RouteStatus.CLOSED)
                .OrderByDescending(l => l.RouteId).Select(l => new Located(l.ParcelId, l.Parcel.Code, l.Parcel.StatusId, l.Route.Code, l.Route.Kind)).FirstOrDefaultAsync(cancellationToken);
        });
        return link ?? throw BusinessException.NotFound($"Le colis {code} n’est pas sur une de vos tournées.", new { reason = "parcel-not-mine", code });
    }
    /// <summary>`CourierStatusService.report` : l'issue passe par le circuit du livreur (encaissement compris).</summary>
    public static async Task<CourierRouteDto> ReportAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, string code, RouteOutcome outcome, CancellationToken cancellationToken)
    {
        var located = await LocateAsync(db, caller, code, cancellationToken);
        if (outcome.StatusId is not { } target) throw BusinessException.Validation("Aucune issue rapportée.");
        if (StatusCatalog.IsLocked(located.StatusId))
        {
            throw BusinessException.Transition("Ce colis est livré : seule l’agence peut revenir sur ce statut.", new { reason = "parcel-locked", from = located.StatusId, to = target, allowed = Array.Empty<int>() });
        }
        var kind = Money.ToWire(located.RouteKind);
        if (!StatusCatalog.CanCourierReport(located.StatusId, target, kind))
        {
            throw BusinessException.Transition($"Un livreur ne peut pas passer ce colis de {located.StatusId} à {target}.",
                new { reason = "courier-transition-forbidden", from = located.StatusId, to = target, allowed = StatusCatalog.CourierOutcomesFor(located.StatusId, kind) });
        }
        await RouteParcelConfirm.RunAsync(db, caller, logger, located.RouteCode, located.ParcelId, outcome with { Confirmed = true }, false, cancellationToken);
        // Le BL collecté à la livraison (`bl.collectOnDelivery`) se branche avec le module des retours et BL.
        return await CourierRouteViews.RouteAsync(db, caller, located.RouteCode, cancellationToken, includeClosed: true);
    }

    /// <summary>`outcomes` : les issues que le livreur peut poser (aucune sur un colis remis sur un bon de retour).</summary>
    public static async Task<IReadOnlyList<int>> OutcomesAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var located = await LocateAsync(db, caller, code, cancellationToken);
        if (located.RouteKind == RouteKind.DELIVERY && await Returns.HandoverRelease.HandedOverOfAsync(db, located.ParcelId, cancellationToken) is not null) return [];
        return StatusCatalog.CourierOutcomesFor(located.StatusId, Money.ToWire(located.RouteKind));
    }
}

/// <summary>`GET /courier/parcels/:code/outcomes` : les issues que le livreur peut poser.</summary>
public sealed record GetCourierOutcomesQuery(string Code) : IRequest<IReadOnlyList<int>>;

internal sealed class GetCourierOutcomesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCourierOutcomesQuery, IReadOnlyList<int>>
{
    public Task<IReadOnlyList<int>> Handle(GetCourierOutcomesQuery request, CancellationToken cancellationToken) => CourierParcels.OutcomesAsync(db, caller, request.Code, cancellationToken);
}

/// <summary>`POST /courier/parcels/:code/status` (`routeConfirmSchema`) : le livreur rapporte une issue, rejouable par `Idempotency-Key`.</summary>
public sealed record ReportCourierOutcomeCommand : RouteConfirmBody, IRequest<object?>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    [JsonIgnore]
    public string? IdempotencyKey { get; init; }
}

internal sealed class ReportCourierOutcomeHandler(IAppDbContext db, ICurrentUser caller, ILogger<ReportCourierOutcomeHandler> logger) : IRequestHandler<ReportCourierOutcomeCommand, object?>
{
    public Task<object?> Handle(ReportCourierOutcomeCommand request, CancellationToken cancellationToken) =>
        IdempotentReplay.RunAsync(db, caller, request.IdempotencyKey, $"POST /courier/parcels/{request.Code}/status", () => ReportAsync(request, cancellationToken), cancellationToken);

    private Task<CourierRouteDto> ReportAsync(ReportCourierOutcomeCommand request, CancellationToken cancellationToken) =>
        CourierParcels.ReportAsync(db, caller, logger, request.Code, request.ToOutcome() with { Confirmed = true }, cancellationToken);
}

public sealed class ReportCourierOutcomeValidator : RouteConfirmBodyValidator<ReportCourierOutcomeCommand>;

/// <summary>`proofPhotoSchema` : une photo de preuve sur la dernière issue posée par ce livreur.</summary>
public sealed record ProofPhotoDto
{
    public string? Name { get; init => field = Zod.Trim(value); }
    public string? MimeType { get; init; }
    public string? Content { get; init; }
}

public sealed record AttachCourierProofCommand : IRequest<object?>
{
    public static readonly string[] Types = ["image/jpeg", "image/png", "image/webp"];

    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    [JsonIgnore]
    public string? IdempotencyKey { get; init; }

    public ProofPhotoDto? Proof { get; init; }
}

public sealed class AttachCourierProofValidator : ZodValidator<AttachCourierProofCommand>
{
    public AttachCourierProofValidator()
    {
        RuleFor(x => x.Proof).Required("object");
        RuleFor(x => x.Proof!.Name).Required().MinLength(1, "required").MaxLength(120).OverridePropertyName("proof.name").When(x => x.Proof is not null);
        RuleFor(x => x.Proof!.MimeType).Must(v => v is not null && AttachCourierProofCommand.Types.Contains(v)).WithMessage("unsupported-file-type").OverridePropertyName("proof.mimeType")
            .When(x => x.Proof is not null);
        RuleFor(x => x.Proof!.Content).Required().MinLength(1, "required").OverridePropertyName("proof.content").When(x => x.Proof is not null);
    }
}

public sealed record ProofAttachedDto(bool Attached);

internal sealed class AttachCourierProofHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage) : IRequestHandler<AttachCourierProofCommand, object?>
{
    public Task<object?> Handle(AttachCourierProofCommand request, CancellationToken cancellationToken) =>
        IdempotentReplay.RunAsync(db, caller, request.IdempotencyKey, $"POST /courier/parcels/{request.Code}/proof", () => AttachAsync(request, cancellationToken), cancellationToken);

    private async Task<ProofAttachedDto> AttachAsync(AttachCourierProofCommand request, CancellationToken cancellationToken)
    {
        var located = await CourierParcels.LocateAsync(db, caller, request.Code, cancellationToken);
        var evt = await db.ParcelEvents.Where(e => e.ParcelId == located.ParcelId && e.AuthorId == caller.UserId).OrderByDescending(e => e.CreatedAt).FirstOrDefaultAsync(cancellationToken)
                  ?? throw BusinessException.Conflict("Rapportez d’abord une issue : une photo seule ne prouve rien.", new { reason = "no-event-to-attach" });
        byte[] content;
        try
        {
            content = Convert.FromBase64String(request.Proof!.Content!);
        }
        catch (FormatException)
        {
            content = [];
        }
        var stored = await storage.PutAsync($"proofs/{caller.CompanyId}", content, request.Proof!.MimeType!, cancellationToken);
        var previous = evt.AttachmentPath;
        evt.AttachmentName = request.Proof!.Name;
        evt.AttachmentMimeType = request.Proof.MimeType;
        evt.AttachmentPath = stored.Key;
        await db.SaveChangesAsync(cancellationToken);
        if (previous is not null) await storage.RemoveAsync(previous, cancellationToken);
        return new ProofAttachedDto(true);
    }
}

public sealed record ScanResultDto(CourierRouteDto Route, CourierRouteParcelDto Added, bool RouteCreated)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Transferred { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReroutedTo { get; init; }
}

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

/// <summary>`DELETE /courier/me/routes/:code/parcels/:parcelId` : le livreur retire un colis de son brouillon.</summary>
public sealed record RemoveCourierRouteParcelCommand(string Code, string ParcelId) : IRequest<CourierRouteDto>;

internal sealed class RemoveCourierRouteParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveCourierRouteParcelCommand, CourierRouteDto>
{
    public async Task<CourierRouteDto> Handle(RemoveCourierRouteParcelCommand request, CancellationToken cancellationToken)
    {
        var courierId = CourierRouteViews.CourierIdOf(caller);
        var route = await db.Routes.AsNoTracking().FirstOrDefaultAsync(r => r.CourierId == courierId && r.Code == request.Code, cancellationToken)
                    ?? throw BusinessException.NotFound($"Tournée introuvable : {request.Code}");
        if (route.Status != RouteStatus.DRAFT)
        {
            throw BusinessException.Conflict("Cette tournée est partie : elle ne se recompose plus depuis le téléphone.", new { reason = "route-dispatched" });
        }
        await RouteWrites.RemoveParcelFromDraftAsync(db, caller, route, request.ParcelId, "courier", cancellationToken);
        return await CourierRouteViews.RouteAsync(db, caller, request.Code, cancellationToken);
    }
}

/// <summary>`POST /courier/me/routes/:code/dispatch` : le livreur fait partir sa tournée.</summary>
public sealed record DispatchCourierRouteCommand(string Code) : IRequest<CourierRouteDto>;

internal sealed class DispatchCourierRouteHandler(IAppDbContext db, ICurrentUser caller, ILogger<DispatchCourierRouteHandler> logger) : IRequestHandler<DispatchCourierRouteCommand, CourierRouteDto>
{
    public async Task<CourierRouteDto> Handle(DispatchCourierRouteCommand request, CancellationToken cancellationToken)
    {
        var courierId = CourierRouteViews.CourierIdOf(caller);
        if (!await db.Routes.AnyAsync(r => r.CourierId == courierId && r.Code == request.Code, cancellationToken)) throw BusinessException.NotFound($"Tournée introuvable : {request.Code}");
        await BulkJobs.BulkJobWrites.AssertIdleAsync(db, request.Code, cancellationToken);
        await RouteDispatch.RunAsync(db, caller, logger, request.Code, new RouteDispatch.Options(), cancellationToken);
        return await CourierRouteViews.RouteAsync(db, caller, request.Code, cancellationToken);
    }
}

public sealed record CourierDeliveryRateDto(int Delivered, int Total, int Percent);

public sealed record CourierPerformanceDto(string Period, int ToPickup, int ToDeliver, int Delivered, int Returning, int Failed, long CollectedInCents, long ToRemitInCents, CourierDeliveryRateDto DeliveryRate);

/// <summary>`GET /courier/me/performance` (`period` : jour ou mois, à Casablanca).</summary>
public sealed record GetCourierPerformanceQuery(string? Period) : IRequest<CourierPerformanceDto>;

public sealed class GetCourierPerformanceValidator : ZodValidator<GetCourierPerformanceQuery>
{
    public GetCourierPerformanceValidator()
    {
        RuleFor(x => x.Period).OneOf(["day", "month"]);
    }
}

internal sealed class GetCourierPerformanceHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetCourierPerformanceQuery, CourierPerformanceDto>
{
    public async Task<CourierPerformanceDto> Handle(GetCourierPerformanceQuery request, CancellationToken cancellationToken)
    {
        var courierId = caller.CourierId ?? throw BusinessException.Forbidden("Ce compte n’est pas rattaché à une fiche livreur.");
        var period = request.Period ?? "day";
        var since = period == "day" ? clock.TodayStartUtc() : Periods.ZonedDayStart(Periods.FirstOfMonth(clock.Today()), ClockDays.Zone);
        var mine = db.Parcels.AsNoTracking().Where(p => p.CourierId == courierId && p.DeletedAt == null);
        var settled = await mine.Where(p => p.StatusAt >= since).Select(p => new { p.StatusId, p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid }).ToListAsync(cancellationToken);
        var pending = await mine.GroupBy(p => p.StatusId).Select(g => new { StatusId = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var outstanding = await mine.Where(p => p.StatusId == CourierPay.Delivered && !p.ReimbursedByCourier)
            .Select(p => new { p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid }).ToListAsync(cancellationToken);
        int By(Func<int, bool> predicate) => pending.Where(r => predicate(r.StatusId)).Sum(r => r.Count);
        // Bug Speedy corrigé : `collected()` ignorait « déjà payé » ; le dû vient de `codToCollect`, comme partout.
        long Due(decimal cod, decimal fee, bool included, bool paid) => Billing.CodToCollect(Money.ToCents(cod), Money.ToCents(fee), included, paid);
        var delivered = settled.Where(r => r.StatusId == CourierPay.Delivered).ToList();
        return new CourierPerformanceDto(period,
            By(id => id is NoteRules.AwaitingPickup or NoteRules.PickupInProgress),
            By(id => id is RouteStatusIds.OutForDelivery or DeliveryCancel.AwaitingDelivery),
            By(id => id == CourierPay.Delivered),
            By(id => StatusCatalog.GroupOf(id) == "return"),
            By(id => StatusCatalog.GroupOf(id) == "failure"),
            delivered.Sum(r => Due(r.CodAmount, r.DeliveryFee, r.FeeIncluded, r.AlreadyPaid)),
            outstanding.Sum(r => Due(r.CodAmount, r.DeliveryFee, r.FeeIncluded, r.AlreadyPaid)),
            new CourierDeliveryRateDto(delivered.Count, settled.Count, settled.Count == 0 ? 0 : (int)Math.Round(delivered.Count * 100.0 / settled.Count, MidpointRounding.AwayFromZero)));
    }
}

public sealed record CourierPickupNoteDto(string Id, string Code, string Kind, string Status, string ClientName, string ClientPhone, string Address, string CityName, int ParcelCount, int PickedCount,
    int RemovedCount, DateTime CreatedAt, DateTime? ValidatedAt);

/// <summary>`GET /courier/me/notes` (`courierNotesQuerySchema`) : les bons qui portent des colis de ce livreur.</summary>
public sealed record GetCourierNotesQuery(string? Kind, string? Status) : IRequest<IReadOnlyList<CourierPickupNoteDto>>;

public sealed class GetCourierNotesValidator : ZodValidator<GetCourierNotesQuery>
{
    public GetCourierNotesValidator()
    {
        RuleFor(x => x.Kind).OneOf(["pickup", "dropoff"]);
        RuleFor(x => x.Status).OneOf(["pending", "done"]);
    }
}

internal sealed class GetCourierNotesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCourierNotesQuery, IReadOnlyList<CourierPickupNoteDto>>
{
    public async Task<IReadOnlyList<CourierPickupNoteDto>> Handle(GetCourierNotesQuery request, CancellationToken cancellationToken)
    {
        var courierId = caller.CourierId ?? throw BusinessException.Forbidden("Ce compte n’est pas rattaché à une fiche livreur.");
        var type = request.Kind == "dropoff" ? DeliveryNoteType.DROPOFF : DeliveryNoteType.PICKUP;
        var notes = db.DeliveryNotes.AsNoTracking().Where(n => n.Type == type && n.Parcels.Any(l => l.Parcel.CourierId == courierId && l.Parcel.DeletedAt == null));
        var open = request.Status != "done" ? await Of(notes.Where(n => n.Status != DeliveryNoteStatus.VALIDATED), courierId, NoteRules.SelectionMax, cancellationToken) : [];
        var done = request.Status != "pending" ? await Of(notes.Where(n => n.Status == DeliveryNoteStatus.VALIDATED), courierId, 100, cancellationToken) : [];
        return [.. open, .. done];
    }

    private static Task<List<CourierPickupNoteDto>> Of(IQueryable<DeliveryNote> notes, string courierId, int take, CancellationToken cancellationToken) =>
        notes.OrderByDescending(n => n.CreatedAt).Take(take).Select(n => new CourierPickupNoteDto(n.Id, n.Code, n.Type == DeliveryNoteType.PICKUP ? "pickup" : "dropoff",
            n.Status == DeliveryNoteStatus.AWAITING_VALIDATION ? "awaiting-validation" : n.Status == DeliveryNoteStatus.PICKED_UP_BY_COURIER ? "picked-up-by-courier" : "validated",
            n.Client.Name, n.Client.Phone, n.Client.Address ?? string.Empty, n.LocationCity.Name,
            n.Parcels.Count(l => l.Parcel.CourierId == courierId && l.Parcel.DeletedAt == null),
            n.Parcels.Count(l => l.Parcel.CourierId == courierId && l.Parcel.DeletedAt == null && l.Parcel.StatusId != NoteRules.AwaitingPickup && l.Parcel.StatusId != NoteRules.PickupInProgress),
            n.RemovedParcels.Count(r => r.Parcel.CourierId == courierId && r.RestoredAt == null), n.CreatedAt, n.ValidatedAt)).ToListAsync(cancellationToken);
}
