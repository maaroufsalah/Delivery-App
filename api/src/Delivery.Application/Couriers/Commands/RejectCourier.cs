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

/// <summary>`POST /couriers/:id/reject` : motif obligatoire, le compte se ferme.</summary>
public sealed record RejectCourierCommand : IRequest<CourierDetailDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public string? Reason { get; init => field = Zod.Trim(value); }
}

public sealed class RejectCourierValidator : ZodValidator<RejectCourierCommand>
{
    public RejectCourierValidator() => RuleFor(x => x.Reason).Required().MinLength(1, "required").MaxLength(500);
}

internal sealed class RejectCourierHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IMapper mapper) : IRequestHandler<RejectCourierCommand, CourierDetailDto>
{
    public async Task<CourierDetailDto> Handle(RejectCourierCommand request, CancellationToken cancellationToken)
    {
        var courier = await CourierRows.FindAsync(db, request.Id, cancellationToken);
        var now = clock.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        courier.RejectedAt = now;
        courier.RejectedById = caller.UserId;
        courier.RejectionReason = request.Reason;
        courier.ValidatedAt = null;
        courier.ValidatedById = null;
        courier.IsActive = false;
        if (courier.UserId is not null)
        {
            var user = await db.Users.FirstAsync(u => u.Id == courier.UserId, cancellationToken);
            user.CloseOnRejection(now);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await CourierRows.DetailAsync(db, mapper, request.Id, cancellationToken);
    }
}
