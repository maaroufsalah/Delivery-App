using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Users.Dtos;
using Delivery.Application.Zones.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Zones.Commands;

/// <summary>`POST /zones` : une zone, ses tarifs et ses villes.</summary>
public sealed record CreateZoneCommand : ZoneFields, IRequest<ZoneListItemDto>;

public sealed class CreateZoneValidator : ZoneFieldsValidator<CreateZoneCommand>;

internal sealed class CreateZoneHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<CreateZoneCommand, ZoneListItemDto>
{
    public async Task<ZoneListItemDto> Handle(CreateZoneCommand input, CancellationToken cancellationToken)
    {
        var cityIds = await ZoneRows.KnownCityIdsAsync(db, input.CityIds!, cancellationToken);
        var zone = new Zone
        {
            Name = input.Name!,
            BaseRate = Money.FromMad(input.BaseRate!.Value),
            SameCityRate = Money.FromMad(input.SameCityRate!.Value),
            DeliveryDays = (int)(input.DeliveryDays ?? 2),
            IsActive = input.IsActive ?? true,
        };
        foreach (var cityId in cityIds) zone.CityLinks.Add(new ZoneCity { ZoneId = zone.Id, CityId = cityId });
        db.Zones.Add(zone);
        await db.SaveChangesAsync(cancellationToken);
        return await ZoneRows.LoadAsync(db, mapper, zone.Id, cancellationToken);
    }
}
