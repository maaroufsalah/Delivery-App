using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Users.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Districts.Commands;

/// <summary>`POST /districts`.</summary>
public sealed record CreateDistrictCommand : DistrictFields, IRequest<DistrictListItemDto>;

public sealed class CreateDistrictValidator : DistrictFieldsValidator<CreateDistrictCommand>;

internal sealed class CreateDistrictHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<CreateDistrictCommand, DistrictListItemDto>
{
    public async Task<DistrictListItemDto> Handle(CreateDistrictCommand input, CancellationToken cancellationToken)
    {
        await DistrictRows.AssertCityExistsAsync(db, input.CityId!, cancellationToken);
        await DistrictRows.AssertNameFreeAsync(db, input.CityId!, input.Name!, null, cancellationToken);
        var district = new District { CityId = input.CityId!, Name = input.Name!, IsActive = input.IsActive ?? true };
        db.Districts.Add(district);
        await db.SaveChangesAsync(cancellationToken);
        return await DistrictRows.LoadAsync(db, mapper, district.Id, cancellationToken);
    }
}
