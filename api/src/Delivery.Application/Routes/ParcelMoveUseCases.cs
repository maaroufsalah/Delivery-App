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
        await DeliveryCancel.SettleAsync(db, logger, plan, cancellationToken);
        return new CancelDeliveryResultDto(plan.Code, plan.Target, plan.Route?.Code, plan.Reopen, remittance, plan.SlipId is not null);
    }
}

/// <summary>`statusCorrectionSchema`.</summary>
public abstract record StatusCorrectionRequest
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public JsonElement? StatusId { get; init; }
    public string? Reason { get; init => field = Zod.Trim(value); }
    public string? CourierId { get; init => field = Zod.Trim(value); }
    public string? RouteCode { get; init => field = Zod.Trim(value); }
    public string? ScheduledDate { get; init; }
    public string? Fingerprint { get; init => field = Zod.Trim(value); }

    public CorrectionInput ToInput() => new((int)JsCoerce.Number(StatusId), Reason ?? string.Empty, CourierId, RouteCode, ScheduledDate, Fingerprint);
}

public abstract class StatusCorrectionValidator<T> : ZodValidator<T> where T : StatusCorrectionRequest
{
    protected StatusCorrectionValidator()
    {
        RuleFor(x => x.StatusId).Custom((value, context) =>
        {
            var number = JsCoerce.Number(value);
            var issue = double.IsNaN(number) ? "Invalid input: expected number, received NaN"
                : Math.Floor(number) != number || double.IsInfinity(number) ? Zod.ExpectedInt
                : number <= 0 ? "required"
                : null;
            if (issue is not null) context.AddFailure(ValidationPaths.Raw + "statusId", issue);
        });
        RuleFor(x => x.Reason).Required().MinLength(1, "required").MaxLength(500, "too-long");
    }
}

public sealed record PreviewStatusCorrectionCommand : StatusCorrectionRequest, IRequest<StatusCorrectionPreviewDto>;

public sealed class PreviewStatusCorrectionValidator : StatusCorrectionValidator<PreviewStatusCorrectionCommand>;

internal sealed class PreviewStatusCorrectionHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<PreviewStatusCorrectionCommand, StatusCorrectionPreviewDto>
{
    public Task<StatusCorrectionPreviewDto> Handle(PreviewStatusCorrectionCommand input, CancellationToken cancellationToken)
    {
        StatusCorrection.AssertAdmin(caller);
        return StatusCorrection.PreviewAsync(db, input.Code, input.ToInput(), cancellationToken);
    }
}

public sealed record ApplyStatusCorrectionCommand : StatusCorrectionRequest, IRequest<StatusCorrectionResultDto>;

public sealed class ApplyStatusCorrectionValidator : StatusCorrectionValidator<ApplyStatusCorrectionCommand>;

internal sealed class ApplyStatusCorrectionHandler(IAppDbContext db, ICurrentUser caller, IClock clock, ILogger<ApplyStatusCorrectionHandler> logger)
    : IRequestHandler<ApplyStatusCorrectionCommand, StatusCorrectionResultDto>
{
    public Task<StatusCorrectionResultDto> Handle(ApplyStatusCorrectionCommand input, CancellationToken cancellationToken)
    {
        StatusCorrection.AssertAdmin(caller);
        return StatusCorrection.ApplyAsync(db, caller, clock, logger, input.Code, input.ToInput(), cancellationToken);
    }
}
