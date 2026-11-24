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

/// <summary>`POST /couriers` : fiche validée d'emblée ; à défaut, l'agence de l'agent qui la crée.</summary>
public sealed record CreateCourierCommand : CourierFields, IRequest<CourierDetailDto>;

public sealed class CreateCourierValidator : CourierFieldsValidator<CreateCourierCommand>;

internal sealed class CreateCourierHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IMapper mapper) : IRequestHandler<CreateCourierCommand, CourierDetailDto>
{
    public async Task<CourierDetailDto> Handle(CreateCourierCommand input, CancellationToken cancellationToken)
    {
        await CourierChecks.AssertCityExistsAsync(db, input.CityId!, cancellationToken);
        if (!string.IsNullOrEmpty(input.AgencyId)) await CourierChecks.AssertAgencyExistsAsync(db, input.AgencyId, cancellationToken);
        var courier = new Courier { ValidatedAt = clock.UtcNow, ValidatedById = caller.UserId };
        CourierRows.Apply(courier, input, caller.AgencyId);
        db.Couriers.Add(courier);
        await db.SaveChangesAsync(cancellationToken);
        return await CourierRows.DetailAsync(db, mapper, courier.Id, cancellationToken);
    }
}
