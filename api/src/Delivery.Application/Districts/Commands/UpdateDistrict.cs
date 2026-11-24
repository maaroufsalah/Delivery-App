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

/// <summary>`PATCH /districts/:id`.</summary>
public sealed record UpdateDistrictCommand : DistrictFields, IRequest<DistrictListItemDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;
}

public sealed class UpdateDistrictValidator : DistrictFieldsValidator<UpdateDistrictCommand>;

internal sealed class UpdateDistrictHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<UpdateDistrictCommand, DistrictListItemDto>
{
    public async Task<DistrictListItemDto> Handle(UpdateDistrictCommand input, CancellationToken cancellationToken)
    {
        var district = await db.Districts.FirstOrDefaultAsync(d => d.Id == input.Id && d.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Quartier introuvable.");
        await DistrictRows.AssertCityExistsAsync(db, input.CityId!, cancellationToken);
        await DistrictRows.AssertNameFreeAsync(db, input.CityId!, input.Name!, input.Id, cancellationToken);
        district.CityId = input.CityId!;
        district.Name = input.Name!;
        district.IsActive = input.IsActive ?? true;
        await db.SaveChangesAsync(cancellationToken);
        return await DistrictRows.LoadAsync(db, mapper, district.Id, cancellationToken);
    }
}
