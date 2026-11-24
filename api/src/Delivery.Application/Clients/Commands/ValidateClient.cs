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

/// <summary>`POST /clients/:id/validate` : valide une inscription et ouvre le compte du vendeur.</summary>
public sealed record ValidateClientCommand : IRequest<ClientDetailDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public string? AgencyId { get; init; }
}

public sealed class ValidateClientValidator : ZodValidator<ValidateClientCommand>
{
    public ValidateClientValidator() => RuleFor(x => x.AgencyId).Must(a => a is null || a.Length >= 0).WithMessage(Zod.InvalidInput);
}

internal sealed class ValidateClientHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IMapper mapper) : IRequestHandler<ValidateClientCommand, ClientDetailDto>
{
    public async Task<ClientDetailDto> Handle(ValidateClientCommand request, CancellationToken cancellationToken)
    {
        var client = await ClientRows.FindAsync(db, request.Id, cancellationToken);
        var agencyId = Zod.EmptyToNull(request.AgencyId) ?? client.AgencyId
            ?? throw BusinessException.Validation("Choisissez une agence pour ce client.", new { field = "agencyId" });
        await ClientRows.AssertAgencyExistsAsync(db, agencyId, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        client.ValidatedAt = clock.UtcNow;
        client.ValidatedById = caller.UserId;
        client.AgencyId = agencyId;
        client.IsActive = true;
        client.RejectedAt = null;
        client.RejectedById = null;
        client.RejectionReason = null;
        if (client.UserId is not null)
        {
            var user = await db.Users.Include(u => u.Agencies).FirstAsync(u => u.Id == client.UserId, cancellationToken);
            user.OpenOnValidation(agencyId);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ClientRows.DetailAsync(db, caller, mapper, request.Id, cancellationToken);
    }
}
