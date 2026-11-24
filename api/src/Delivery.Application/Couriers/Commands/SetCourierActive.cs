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

/// <summary>`PATCH /couriers/:id/active`.</summary>
public sealed record SetCourierActiveCommand : IRequest<CourierDetailDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public bool? IsActive { get; init; }
}

public sealed class SetCourierActiveValidator : ZodValidator<SetCourierActiveCommand>
{
    public SetCourierActiveValidator() => RuleFor(x => x.IsActive).Required("boolean");
}

internal sealed class SetCourierActiveHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<SetCourierActiveCommand, CourierDetailDto>
{
    public async Task<CourierDetailDto> Handle(SetCourierActiveCommand request, CancellationToken cancellationToken)
    {
        var courier = await CourierRows.FindAsync(db, request.Id, cancellationToken);
        courier.IsActive = request.IsActive!.Value;
        await db.SaveChangesAsync(cancellationToken);
        return await CourierRows.DetailAsync(db, mapper, courier.Id, cancellationToken);
    }
}
