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

/// <summary>`PATCH /zones/:id/active`.</summary>
public sealed record SetZoneActiveCommand : IRequest<ZoneListItemDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public bool? IsActive { get; init; }
}

public sealed class SetZoneActiveValidator : ZodValidator<SetZoneActiveCommand>
{
    public SetZoneActiveValidator() => RuleFor(x => x.IsActive).Required("boolean");
}

internal sealed class SetZoneActiveHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<SetZoneActiveCommand, ZoneListItemDto>
{
    public async Task<ZoneListItemDto> Handle(SetZoneActiveCommand request, CancellationToken cancellationToken)
    {
        var zone = await db.Zones.FirstOrDefaultAsync(z => z.Id == request.Id && z.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Zone introuvable.");
        zone.IsActive = request.IsActive!.Value;
        await db.SaveChangesAsync(cancellationToken);
        return await ZoneRows.LoadAsync(db, mapper, zone.Id, cancellationToken);
    }
}
