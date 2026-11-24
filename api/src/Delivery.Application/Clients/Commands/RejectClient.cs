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

/// <summary>`POST /clients/:id/reject` : refuse une inscription et ferme le compte.</summary>
public sealed record RejectClientCommand : IRequest<ClientDetailDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public string? Reason { get; init => field = Zod.Trim(value); }
}

public sealed class RejectClientValidator : ZodValidator<RejectClientCommand>
{
    public RejectClientValidator() => RuleFor(x => x.Reason).Required().MinLength(1, "required").MaxLength(500);
}

internal sealed class RejectClientHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IMapper mapper) : IRequestHandler<RejectClientCommand, ClientDetailDto>
{
    public async Task<ClientDetailDto> Handle(RejectClientCommand request, CancellationToken cancellationToken)
    {
        var client = await ClientRows.FindAsync(db, request.Id, cancellationToken);
        var now = clock.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        client.RejectedAt = now;
        client.RejectedById = caller.UserId;
        client.RejectionReason = request.Reason;
        client.ValidatedAt = null;
        client.ValidatedById = null;
        client.IsActive = false;
        if (client.UserId is not null)
        {
            var user = await db.Users.FirstAsync(u => u.Id == client.UserId, cancellationToken);
            user.CloseOnRejection(now);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ClientRows.DetailAsync(db, caller, mapper, request.Id, cancellationToken);
    }
}
