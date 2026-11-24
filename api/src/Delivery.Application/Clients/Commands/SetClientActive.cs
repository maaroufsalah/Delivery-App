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

/// <summary>`PATCH /clients/:id/active`.</summary>
public sealed record SetClientActiveCommand : IRequest<ClientDetailDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public bool? IsActive { get; init; }
}

public sealed class SetClientActiveValidator : ZodValidator<SetClientActiveCommand>
{
    public SetClientActiveValidator() => RuleFor(x => x.IsActive).Required("boolean");
}

internal sealed class SetClientActiveHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<SetClientActiveCommand, ClientDetailDto>
{
    public async Task<ClientDetailDto> Handle(SetClientActiveCommand request, CancellationToken cancellationToken)
    {
        var client = await ClientRows.FindAsync(db, request.Id, cancellationToken);
        client.IsActive = request.IsActive!.Value;
        await db.SaveChangesAsync(cancellationToken);
        return await ClientRows.DetailAsync(db, caller, mapper, request.Id, cancellationToken);
    }
}
