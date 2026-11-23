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

/// <summary>`POST /cities` : une ville et son rattachement de zone.</summary>
public sealed record CreateCityCommand : CityFields, IRequest<CityListItemDto>;

public sealed class CreateCityValidator : CityFieldsValidator<CreateCityCommand>;

internal sealed class CreateCityHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<CreateCityCommand, CityListItemDto>
{
    public async Task<CityListItemDto> Handle(CreateCityCommand input, CancellationToken cancellationToken)
    {
        await CityRows.AssertZoneExistsAsync(db, input.ZoneId!, cancellationToken);
        await CityRows.AssertCodeFreeAsync(db, input.Code!, null, cancellationToken);
        var city = new City
        {
            Name = input.Name!,
            NameAr = string.IsNullOrEmpty(input.NameAr) ? null : input.NameAr,
            Code = input.Code!,
            ZoneId = input.ZoneId!,
            IsActive = input.IsActive ?? true,
        };
        city.ZoneLinks.Add(new ZoneCity { ZoneId = input.ZoneId!, CityId = city.Id });
        db.Cities.Add(city);
        await db.SaveChangesAsync(cancellationToken);
        return await CityRows.LoadAsync(db, mapper, city.Id, cancellationToken);
    }
}
