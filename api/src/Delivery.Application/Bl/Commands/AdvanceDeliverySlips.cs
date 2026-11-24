using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Bl;

/// <summary>`deliverySlipAdvanceSchema` : réceptionner, ou remettre au vendeur, des BL cochés ou « tout ».</summary>
public sealed record AdvanceDeliverySlipsCommand : IRequest<DeliverySlipAdvanceResultDto>
{
    public string? To { get; init; }
    public IReadOnlyList<string>? Ids { get; init; }
    public bool? All { get; init; }
    public string? Search { get; init => field = Zod.Trim(value); }
    public IReadOnlyList<string>? Exclude { get; init; }
    public DeliverySlipFiltersBody? Filters { get; init; }
    public string? ReturnedToName { get; init => field = Zod.Trim(value); }
}

public sealed class AdvanceDeliverySlipsValidator : ZodValidator<AdvanceDeliverySlipsCommand>
{
    public AdvanceDeliverySlipsValidator()
    {
        RuleFor(x => x.To).Required().OneOf(["received", "returned"]);
        RuleFor(x => x.ReturnedToName).MinLength(2, "too-short").MaxLength(120);
        RuleFor(x => x.Search).MaxLength(120);
    }

    protected override void Refine(AdvanceDeliverySlipsCommand value, Action<string, string> issue)
    {
        if (value.All != true && (value.Ids?.Count ?? 0) == 0) issue(ValidationPaths.Raw + "ids", "required");
        if (value.To == "returned" && string.IsNullOrEmpty(value.ReturnedToName)) issue(ValidationPaths.Raw + "returnedToName", "required");
    }
}

internal sealed class AdvanceDeliverySlipsHandler(IAppDbContext db, ICurrentUser caller, ILogger<AdvanceDeliverySlipsHandler> logger) : IRequestHandler<AdvanceDeliverySlipsCommand, DeliverySlipAdvanceResultDto>
{
    public async Task<DeliverySlipAdvanceResultDto> Handle(AdvanceDeliverySlipsCommand input, CancellationToken cancellationToken)
    {
        var f = input.Filters ?? new DeliverySlipFiltersBody();
        var filter = new DeliverySlipFilter(f.Search, f.Status, f.ClientId is null ? null : [f.ClientId], f.CourierId is null ? null : [f.CourierId], f.From, f.To);
        var ids = await NoteRules.ResolveSelectionAsync(input.Ids, input.All, input.Exclude, async take =>
            await (await BlRows.FilterAsync(db, caller, filter, cancellationToken)).OrderByDescending(s => s.CollectedAt).ThenBy(s => s.Id).Select(s => s.Id).Take(take).ToListAsync(cancellationToken));
        var to = input.To == "received" ? DeliverySlipStatus.RECEIVED : DeliverySlipStatus.RETURNED;
        var shortName = await BlRows.ShortNameAsync(db, cancellationToken);
        var comment = to == DeliverySlipStatus.RECEIVED ? $"Réceptionné par {shortName}" : $"Récupéré par le vendeur — remis à {input.ReturnedToName ?? string.Empty}";
        List<BlRows.Moved> moved;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            moved = await BlRows.AdvanceSlipsAsync(db, caller, BlRows.Slips(db, caller).Where(s => ids.Contains(s.Id)), to, comment, $"BL — {comment}", DateTime.UtcNow, input.ReturnedToName,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        await BlRows.NotifySlipStepAsync(db, logger, moved, to, null, shortName, cancellationToken);
        return new DeliverySlipAdvanceResultDto(moved.Count, ids.Count - moved.Count);
    }
}
