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
