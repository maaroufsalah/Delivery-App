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

/// <summary>`PATCH /zones/:id` : les villes listées remplacent le rattachement.</summary>
public sealed record UpdateZoneCommand : ZoneFields, IRequest<ZoneListItemDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;
}

public sealed class UpdateZoneValidator : ZoneFieldsValidator<UpdateZoneCommand>;

internal sealed class UpdateZoneHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<UpdateZoneCommand, ZoneListItemDto>
{
    public async Task<ZoneListItemDto> Handle(UpdateZoneCommand input, CancellationToken cancellationToken)
    {
        var zone = await db.Zones.FirstOrDefaultAsync(z => z.Id == input.Id && z.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Zone introuvable.");
        var cityIds = await ZoneRows.KnownCityIdsAsync(db, input.CityIds!, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        zone.Name = input.Name!;
        zone.BaseRate = Money.FromMad(input.BaseRate!.Value);
        zone.SameCityRate = Money.FromMad(input.SameCityRate!.Value);
        zone.DeliveryDays = (int)(input.DeliveryDays ?? 2);
        zone.IsActive = input.IsActive ?? true;
        await db.SaveChangesAsync(cancellationToken);
        await db.ZoneCities.Where(l => l.ZoneId == input.Id && !cityIds.Contains(l.CityId)).ExecuteDeleteAsync(cancellationToken);
        var existing = await db.ZoneCities.Where(l => l.ZoneId == input.Id).Select(l => l.CityId).ToListAsync(cancellationToken);
        foreach (var cityId in cityIds.Except(existing)) db.ZoneCities.Add(new ZoneCity { ZoneId = input.Id, CityId = cityId });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ZoneRows.LoadAsync(db, mapper, input.Id, cancellationToken);
    }
}
