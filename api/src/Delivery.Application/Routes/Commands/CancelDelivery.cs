using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>`cancelDeliverySchema` : un motif.</summary>
public sealed record CancelDeliveryCommand : IRequest<CancelDeliveryResultDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? Reason { get; init => field = Zod.Trim(value); }
}

public sealed class CancelDeliveryValidator : ZodValidator<CancelDeliveryCommand>
{
    public CancelDeliveryValidator()
    {
        RuleFor(x => x.Reason).Required().MinLength(1, "required").MaxLength(255, "too-long");
    }
}

internal sealed class CancelDeliveryHandler(IAppDbContext db, ICurrentUser caller, IClock clock, ILogger<CancelDeliveryHandler> logger) : IRequestHandler<CancelDeliveryCommand, CancelDeliveryResultDto>
{
    public async Task<CancelDeliveryResultDto> Handle(CancelDeliveryCommand input, CancellationToken cancellationToken)
    {
        DeliveryCancel.AssertAdmin(caller);
        var reason = input.Reason?.Trim() ?? string.Empty;
        if (reason.Length == 0) throw BusinessException.Validation("Une annulation de livraison exige un motif.", new { reason = "reason-required", field = "reason" });
        var plan = await DeliveryCancel.PlanAsync(db, input.Code, cancellationToken);
        var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
        string? remittance;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            remittance = await DeliveryCancel.WriteAsync(db, caller, plan, reason, author, clock.UtcNow, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        await DeliveryCancel.SettleAsync(db, caller, logger, plan, cancellationToken);
        return new CancelDeliveryResultDto(plan.Code, plan.Target, plan.Route?.Code, plan.Reopen, remittance, plan.SlipId is not null);
    }
}
