using System.Text.Json.Nodes;
using AutoMapper;
using Delivery.Application.Clients.Dtos;
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

namespace Delivery.Application.Clients.Commands;

/// <summary>`PATCH /clients/:id/validation` : le corps est `{ isActive }` (schéma de bascule réutilisé).</summary>
public sealed record SetClientValidatedCommand : IRequest<ClientDetailDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public bool? IsActive { get; init; }
}

public sealed class SetClientValidatedValidator : ZodValidator<SetClientValidatedCommand>
{
    public SetClientValidatedValidator() => RuleFor(x => x.IsActive).Required("boolean");
}

internal sealed class SetClientValidatedHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IMapper mapper) : IRequestHandler<SetClientValidatedCommand, ClientDetailDto>
{
    public async Task<ClientDetailDto> Handle(SetClientValidatedCommand request, CancellationToken cancellationToken)
    {
        var client = await ClientRows.FindAsync(db, request.Id, cancellationToken);
        client.ValidatedAt = request.IsActive == true ? clock.UtcNow : null;
        client.ValidatedById = request.IsActive == true ? caller.UserId : null;
        await db.SaveChangesAsync(cancellationToken);
        return await ClientRows.DetailAsync(db, caller, mapper, request.Id, cancellationToken);
    }
}
