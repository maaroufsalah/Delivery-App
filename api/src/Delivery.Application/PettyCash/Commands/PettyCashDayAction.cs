using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.PettyCash;

/// <summary>`POST /petty-cash/days/:day/close|reopen` (`pettyCashDayCloseSchema`, `pettyCashDayReopenSchema`).</summary>
public sealed record PettyCashDayActionCommand : IRequest<PettyCashDayDetailDto>
{
    [JsonIgnore]
    public string Day { get; init; } = string.Empty;

    [JsonIgnore]
    public bool Reopen { get; init; }

    public string? AgencyId { get; init => field = Zod.Trim(value); }
    public string? Reason { get; init => field = Zod.Trim(value); }
}

public sealed class PettyCashDayActionValidator : ZodValidator<PettyCashDayActionCommand>
{
    public PettyCashDayActionValidator()
    {
        RuleFor(x => x.Reason).Required().MinLength(1, "required").MaxLength(300).When(x => x.Reopen);
    }
}

internal sealed class PettyCashDayActionHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<PettyCashDayActionCommand, PettyCashDayDetailDto>
{
    public Task<PettyCashDayDetailDto> Handle(PettyCashDayActionCommand input, CancellationToken cancellationToken)
    {
        DayParam.Check(input.Day);
        var agencyId = string.IsNullOrEmpty(input.AgencyId) ? null : input.AgencyId;
        return input.Reopen
            ? PettyCashDays.ReopenAsync(db, caller, input.Day, agencyId, input.Reason!, cancellationToken)
            : PettyCashDays.CloseAsync(db, caller, input.Day, agencyId, cancellationToken);
    }
}
