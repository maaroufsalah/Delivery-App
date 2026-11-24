using System.Text.Json.Nodes;
using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Users;
using Delivery.Application.Users.Commands;
using Delivery.Application.Users.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Couriers.Commands;

/// <summary>`PUT /couriers/:id/cities` : la grille de paie par ville, remplacée en bloc et tracée.</summary>
public sealed record SetCourierCitiesCommand : IRequest<CourierDetailDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public IReadOnlyList<CourierCityInput>? Cities { get; init; }
}

public sealed record CourierCityInput
{
    public string? CityId { get; init; }

    public double? PickupFee { get; init; }

    public double? DeliveryFee { get; init; }
}

public sealed class SetCourierCitiesValidator : ZodValidator<SetCourierCitiesCommand>
{
    public SetCourierCitiesValidator()
    {
        RuleFor(x => x.Cities).Required("array").Must(c => c is null || c.Count <= 5_000).WithMessage("too-many");
        RuleForEach(x => x.Cities).ChildRules(city =>
        {
            city.RuleFor(c => c.CityId).Required().MinLength(1, "required");
            city.RuleFor(c => c.PickupFee).MadAmount();
            city.RuleFor(c => c.DeliveryFee).MadAmount();
        });
    }
}

internal sealed class SetCourierCitiesHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IMapper mapper) : IRequestHandler<SetCourierCitiesCommand, CourierDetailDto>
{
    public async Task<CourierDetailDto> Handle(SetCourierCitiesCommand input, CancellationToken cancellationToken)
    {
        await CourierRows.FindAsync(db, input.Id, cancellationToken);
        var cityIds = input.Cities!.Select(c => c.CityId!).ToList();
        if (cityIds.Distinct().Count() != cityIds.Count) throw BusinessException.Validation("Une ville apparaît deux fois dans la grille.", new { field = "cities" });
        if (cityIds.Count > 0 && await db.Cities.CountAsync(c => cityIds.Contains(c.Id) && c.DeletedAt == null, cancellationToken) != cityIds.Count)
        {
            throw BusinessException.Validation("Ville inconnue dans la grille.", new { field = "cities" });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var before = await db.CourierCities.Where(c => c.CourierId == input.Id).OrderBy(c => c.CityId).ToListAsync(cancellationToken);
        static JsonObject Grid(IEnumerable<(string CityId, long PickupFee, long DeliveryFee)> rows) => new()
        {
            ["cities"] = new JsonArray(rows.OrderBy(r => r.CityId, StringComparer.Ordinal)
                .Select(r => (JsonNode)new JsonObject { ["cityId"] = r.CityId, ["pickupFee"] = r.PickupFee, ["deliveryFee"] = r.DeliveryFee }).ToArray()),
        };
        db.AuditLogs.Add(new AuditLog
        {
            Entity = "Courier",
            EntityId = input.Id,
            Action = AuditAction.UPDATE,
            Before = Grid(before.Select(r => (r.CityId, Money.ToCents(r.PickupFee), Money.ToCents(r.DeliveryFee)))).ToJsonString(),
            After = Grid(input.Cities!.Select(c => (c.CityId!, Money.MadToCents(c.PickupFee!.Value), Money.MadToCents(c.DeliveryFee!.Value)))).ToJsonString(),
            Diff = ["cities"],
            UserId = caller.UserId,
            UserEmail = caller.Email,
            Role = caller.Roles.FirstOrDefault().ToString(),
            Ip = caller.IpAddress,
            UserAgent = caller.UserAgent,
            RequestId = caller.RequestId,
            Source = caller.IsMobileClient ? "mobile" : "web",
            At = clock.UtcNow,
        });
        db.CourierCities.RemoveRange(before);
        await db.SaveChangesAsync(cancellationToken);
        db.CourierCities.AddRange(input.Cities!.Select(c => new CourierCity
        {
            CourierId = input.Id,
            CityId = c.CityId!,
            PickupFee = Money.FromMad(c.PickupFee!.Value),
            DeliveryFee = Money.FromMad(c.DeliveryFee!.Value),
        }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await CourierRows.DetailAsync(db, mapper, input.Id, cancellationToken);
    }
}
