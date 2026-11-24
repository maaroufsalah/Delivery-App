using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>`routeUpdateSchema` : ville, livreur, type (circuit vide) et date de sortie d'un brouillon.</summary>
public sealed record UpdateRouteCommand : IRequest<RouteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? CityId { get; init; }
    public string? CourierId { get; init; }
    public string? Kind { get; init; }
    public string? ExitDate { get; init => field = Zod.Trim(value); }
}

public sealed class UpdateRouteValidator : ZodValidator<UpdateRouteCommand>
{
    public UpdateRouteValidator()
    {
        RuleFor(x => x.CityId).Must(v => v is null || v.Length >= 1).WithMessage("required");
        RuleFor(x => x.CourierId).Must(v => v is null || v.Length >= 1).WithMessage("required");
        RuleFor(x => x.Kind).OneOf(CreateRouteCommand.Kinds);
    }

    protected override void Refine(UpdateRouteCommand value, Action<string, string> issue)
    {
        if (value.CityId is null && value.CourierId is null && value.Kind is null && value.ExitDate is null) issue(string.Empty, "required");
    }
}

internal sealed class UpdateRouteHandler(IAppDbContext db, ICurrentUser caller, ILogger<UpdateRouteHandler> logger) : IRequestHandler<UpdateRouteCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(UpdateRouteCommand input, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, input.Code, cancellationToken, tracking: true);
        RouteWrites.AssertDraft(route.Status);
        var content = await db.RouteParcels.CountAsync(l => l.RouteId == route.Id, cancellationToken) + await db.RouteNotes.CountAsync(l => l.RouteId == route.Id, cancellationToken);
        RouteKind? kind = input.Kind is null ? null : RouteWrites.KindOf(input.Kind);
        var reshaped = (input.CityId is not null && input.CityId != route.CityId) || (kind is not null && kind != route.Kind);
        if (reshaped && content > 0)
        {
            throw BusinessException.Conflict($"{route.Code} porte des colis ou des bons : retirez-les avant d’en changer la ville ou le type.", new { reason = "route-not-empty" });
        }
        var courierChanged = input.CourierId is not null && input.CourierId != route.CourierId;
        if (courierChanged && content > 0)
        {
            throw BusinessException.Conflict($"{route.Code} porte des colis ou des bons : retirez-les avant d’en changer le livreur.", new { reason = "route-not-empty" });
        }
        if (input.CityId is not null && !await db.Cities.AnyAsync(c => c.Id == input.CityId && c.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("Ville introuvable.");
        var courier = input.CourierId is null ? null
            : await db.Couriers.AsNoTracking().Where(c => c.Id == input.CourierId && c.DeletedAt == null).Select(c => new { c.Id, c.CityId, Grid = c.Cities.Count }).FirstOrDefaultAsync(cancellationToken)
              ?? throw BusinessException.NotFound("Livreur introuvable.");
        var cityId = courierChanged && courier is { Grid: > 0 } ? courier.CityId : input.CityId;
        var previousCourier = route.CourierId;
        if (cityId is not null && cityId != route.CityId)
        {
            route.CityId = cityId;
            route.OriginCityId = await RouteWrites.OriginCityOfAsync(db, caller, cityId, cancellationToken);
        }
        if (kind is not null && kind != route.Kind) route.Kind = kind.Value;
        if (courierChanged) route.CourierId = input.CourierId!;
        if (!string.IsNullOrEmpty(input.ExitDate)) route.ExitDate = Parcels.Commands.ParcelCreation.ParseDate(input.ExitDate);
        await db.SaveChangesAsync(cancellationToken);
        if (courierChanged) await RouteWrites.NotifyWithdrawnAsync(db, logger, previousCourier, route.Code, cancellationToken);
        return await RouteRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
