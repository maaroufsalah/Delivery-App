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

/// <summary>`PATCH /couriers/:id/validation` : corps `{ isActive }`.</summary>
public sealed record SetCourierValidatedCommand : IRequest<CourierDetailDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public bool? IsActive { get; init; }
}

public sealed class SetCourierValidatedValidator : ZodValidator<SetCourierValidatedCommand>
{
    public SetCourierValidatedValidator() => RuleFor(x => x.IsActive).Required("boolean");
}

internal sealed class SetCourierValidatedHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IMapper mapper) : IRequestHandler<SetCourierValidatedCommand, CourierDetailDto>
{
    public async Task<CourierDetailDto> Handle(SetCourierValidatedCommand request, CancellationToken cancellationToken)
    {
        var courier = await CourierRows.FindAsync(db, request.Id, cancellationToken);
        courier.ValidatedAt = request.IsActive == true ? clock.UtcNow : null;
        courier.ValidatedById = request.IsActive == true ? caller.UserId : null;
        await db.SaveChangesAsync(cancellationToken);
        return await CourierRows.DetailAsync(db, mapper, courier.Id, cancellationToken);
    }
}
