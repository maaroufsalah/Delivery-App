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

/// <summary>`PATCH /cities/:id/active`.</summary>
public sealed record SetCityActiveCommand : IRequest<CityListItemDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public bool? IsActive { get; init; }
}

public sealed class SetCityActiveValidator : ZodValidator<SetCityActiveCommand>
{
    public SetCityActiveValidator() => RuleFor(x => x.IsActive).Required("boolean");
}

internal sealed class SetCityActiveHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<SetCityActiveCommand, CityListItemDto>
{
    public async Task<CityListItemDto> Handle(SetCityActiveCommand request, CancellationToken cancellationToken)
    {
        var city = await db.Cities.FirstOrDefaultAsync(c => c.Id == request.Id && c.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Ville introuvable.");
        city.IsActive = request.IsActive!.Value;
        await db.SaveChangesAsync(cancellationToken);
        return await CityRows.LoadAsync(db, mapper, city.Id, cancellationToken);
    }
}
