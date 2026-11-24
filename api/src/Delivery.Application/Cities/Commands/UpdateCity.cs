using System.Text.RegularExpressions;
using AutoMapper;
using Delivery.Application.Cities.Dtos;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Cities.Commands;

/// <summary>`PATCH /cities/:id` : le code est figé dès le premier colis.</summary>
public sealed record UpdateCityCommand : CityFields, IRequest<CityListItemDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;
}

public sealed class UpdateCityValidator : CityFieldsValidator<UpdateCityCommand>;

internal sealed class UpdateCityHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<UpdateCityCommand, CityListItemDto>
{
    public async Task<CityListItemDto> Handle(UpdateCityCommand input, CancellationToken cancellationToken)
    {
        var existing = await db.Cities.FirstOrDefaultAsync(c => c.Id == input.Id && c.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Ville introuvable.");
        await CityRows.AssertZoneExistsAsync(db, input.ZoneId!, cancellationToken);
        var parcels = (await CityRows.ParcelCountsAsync(db, [input.Id], cancellationToken)).GetValueOrDefault(input.Id);
        if (input.Code != existing.Code && parcels > 0)
        {
            throw BusinessException.Conflict("Le code est figé : des colis l’utilisent déjà.", new { references = new[] { References.Of("parcels", parcels) }, field = "code" });
        }
        if (input.Code != existing.Code) await CityRows.AssertCodeFreeAsync(db, input.Code!, input.Id, cancellationToken);

        var previousZone = existing.ZoneId;
        existing.Name = input.Name!;
        existing.NameAr = string.IsNullOrEmpty(input.NameAr) ? null : input.NameAr;
        existing.Code = input.Code!;
        existing.ZoneId = input.ZoneId!;
        existing.IsActive = input.IsActive ?? true;
        await db.SaveChangesAsync(cancellationToken);
        if (existing.ZoneId != previousZone)
        {
            await db.ZoneCities.Where(z => z.CityId == input.Id && z.ZoneId == previousZone).ExecuteDeleteAsync(cancellationToken);
            if (!await db.ZoneCities.AnyAsync(z => z.CityId == input.Id && z.ZoneId == existing.ZoneId, cancellationToken))
            {
                db.ZoneCities.Add(new ZoneCity { CityId = input.Id, ZoneId = existing.ZoneId });
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        return await CityRows.LoadAsync(db, mapper, input.Id, cancellationToken);
    }
}
