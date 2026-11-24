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

namespace Delivery.Application.Agencies.Commands;

/// <summary>`PATCH /agencies/:id/active`.</summary>
public sealed record SetAgencyActiveCommand : IRequest<AgencyListItemDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public bool? IsActive { get; init; }
}

public sealed class SetAgencyActiveValidator : ZodValidator<SetAgencyActiveCommand>
{
    public SetAgencyActiveValidator() => RuleFor(x => x.IsActive).Required("boolean");
}

internal sealed class SetAgencyActiveHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<SetAgencyActiveCommand, AgencyListItemDto>
{
    public async Task<AgencyListItemDto> Handle(SetAgencyActiveCommand request, CancellationToken cancellationToken)
    {
        var agency = await db.Agencies.FirstOrDefaultAsync(a => a.Id == request.Id && a.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Agence introuvable.");
        if (request.IsActive == false && request.Id == await AgencyRows.HeadquartersIdAsync(db, cancellationToken))
        {
            throw BusinessException.Validation("Le siège ne peut pas être désactivé.", new { field = "isActive" });
        }
        agency.IsActive = request.IsActive!.Value;
        await db.SaveChangesAsync(cancellationToken);
        return await AgencyRows.LoadAsync(db, mapper, agency.Id, cancellationToken);
    }
}
