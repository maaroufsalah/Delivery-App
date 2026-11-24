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

/// <summary>`POST /couriers/:id/validate` : ouvre la fiche ET le compte.</summary>
public sealed record ValidateCourierCommand : IRequest<CourierDetailDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public string? AgencyId { get; init; }
}

internal sealed class ValidateCourierHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IMapper mapper) : IRequestHandler<ValidateCourierCommand, CourierDetailDto>
{
    public async Task<CourierDetailDto> Handle(ValidateCourierCommand request, CancellationToken cancellationToken)
    {
        var courier = await CourierRows.FindAsync(db, request.Id, cancellationToken);
        var agencyId = Zod.EmptyToNull(request.AgencyId) ?? courier.AgencyId
            ?? throw BusinessException.Validation("Choisissez une agence pour ce livreur.", new { field = "agencyId" });
        await CourierChecks.AssertAgencyExistsAsync(db, agencyId, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        courier.ValidatedAt = clock.UtcNow;
        courier.ValidatedById = caller.UserId;
        courier.AgencyId = agencyId;
        courier.IsActive = true;
        courier.RejectedAt = null;
        courier.RejectedById = null;
        courier.RejectionReason = null;
        if (courier.UserId is not null)
        {
            var user = await db.Users.Include(u => u.Agencies).FirstAsync(u => u.Id == courier.UserId, cancellationToken);
            user.OpenOnValidation(agencyId);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await CourierRows.DetailAsync(db, mapper, request.Id, cancellationToken);
    }
}
