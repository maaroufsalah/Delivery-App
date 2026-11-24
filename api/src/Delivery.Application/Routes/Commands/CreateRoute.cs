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

/// <summary>`routeSchema` : un circuit en brouillon, code `CIR-AAMM-NNNN`.</summary>
public sealed record CreateRouteCommand : IRequest<RouteDetailDto>
{
    public static readonly string[] Kinds = ["delivery", "pickup", "return"];

    public string? CityId { get; init; }
    public string? CourierId { get; init; }
    public string? Kind { get; init; } = "delivery";
    public string? ExitDate { get; init => field = Zod.Trim(value); }
}

public sealed class CreateRouteValidator : ZodValidator<CreateRouteCommand>
{
    public CreateRouteValidator()
    {
        RuleFor(x => x.CityId).Required().MinLength(1, "required");
        RuleFor(x => x.CourierId).Required().MinLength(1, "required");
        RuleFor(x => x.Kind).OneOf(CreateRouteCommand.Kinds);
    }
}

internal sealed class CreateRouteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateRouteCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(CreateRouteCommand input, CancellationToken cancellationToken)
    {
        var courier = await db.Couriers.AsNoTracking().Where(c => c.Id == input.CourierId && c.DeletedAt == null).Select(c => new { c.Id, c.CityId, Grid = c.Cities.Count }).FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound("Livreur introuvable.");
        var cityId = courier.Grid > 0 ? courier.CityId : input.CityId!;
        if (!await db.Cities.AnyAsync(c => c.Id == cityId && c.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("Ville introuvable.");
        var origin = await RouteWrites.OriginCityOfAsync(db, caller, cityId, cancellationToken);
        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var code = await DocumentCodes.ReserveAsync(db, RouteRows.Prefix, now, cancellationToken);
        db.Routes.Add(new Route
        {
            Code = code, Status = RouteStatus.DRAFT, StatusAt = now, Kind = RouteWrites.KindOf(input.Kind), CityId = cityId, OriginCityId = origin, CourierId = courier.Id,
            ExitDate = string.IsNullOrEmpty(input.ExitDate) ? now : Parcels.Commands.ParcelCreation.ParseDate(input.ExitDate),
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await RouteRows.DetailAsync(db, caller, code, cancellationToken);
    }
}
