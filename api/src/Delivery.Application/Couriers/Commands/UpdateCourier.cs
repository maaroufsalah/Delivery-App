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

/// <summary>`PATCH /couriers/:id` : la fiche entière (schéma complet, pas un patch).</summary>
public sealed record UpdateCourierCommand : CourierFields, IRequest<CourierDetailDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;
}

public sealed class UpdateCourierValidator : CourierFieldsValidator<UpdateCourierCommand>;

internal sealed class UpdateCourierHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<UpdateCourierCommand, CourierDetailDto>
{
    public async Task<CourierDetailDto> Handle(UpdateCourierCommand input, CancellationToken cancellationToken)
    {
        var courier = await CourierRows.FindAsync(db, input.Id, cancellationToken);
        await CourierChecks.AssertCityExistsAsync(db, input.CityId!, cancellationToken);
        if (!string.IsNullOrEmpty(input.AgencyId)) await CourierChecks.AssertAgencyExistsAsync(db, input.AgencyId, cancellationToken);
        CourierRows.Apply(courier, input, null);
        await db.SaveChangesAsync(cancellationToken);
        return await CourierRows.DetailAsync(db, mapper, courier.Id, cancellationToken);
    }
}
