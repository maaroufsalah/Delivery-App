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

/// <summary>`PATCH /districts/:id/active`.</summary>
public sealed record SetDistrictActiveCommand : IRequest<DistrictListItemDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public bool? IsActive { get; init; }
}

public sealed class SetDistrictActiveValidator : ZodValidator<SetDistrictActiveCommand>
{
    public SetDistrictActiveValidator() => RuleFor(x => x.IsActive).Required("boolean");
}

internal sealed class SetDistrictActiveHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<SetDistrictActiveCommand, DistrictListItemDto>
{
    public async Task<DistrictListItemDto> Handle(SetDistrictActiveCommand request, CancellationToken cancellationToken)
    {
        var district = await db.Districts.FirstOrDefaultAsync(d => d.Id == request.Id && d.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Quartier introuvable.");
        district.IsActive = request.IsActive!.Value;
        await db.SaveChangesAsync(cancellationToken);
        return await DistrictRows.LoadAsync(db, mapper, district.Id, cancellationToken);
    }
}
