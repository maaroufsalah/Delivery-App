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

/// <summary>`PATCH /petty-cash/:id/validate|refuse` : une dépense de livreur en brouillon est tranchée.</summary>
public sealed record DecidePettyCashCommand : IRequest<PettyCashMovementDto>
{
    [JsonIgnore]
    public string Id { get; init; } = string.Empty;

    [JsonIgnore]
    public bool Validate { get; init; }

    public string? Reason { get; init => field = Zod.Trim(value); }
}

public sealed class DecidePettyCashValidator : ZodValidator<DecidePettyCashCommand>
{
    public DecidePettyCashValidator()
    {
        RuleFor(x => x.Reason).MaxLength(300);
    }
}

internal sealed class DecidePettyCashHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<DecidePettyCashCommand, PettyCashMovementDto>
{
    public async Task<PettyCashMovementDto> Handle(DecidePettyCashCommand input, CancellationToken cancellationToken)
    {
        var scoped = PettyCashMovements.Scoped(db.PettyCashMovements.AsNoTracking(), caller);
        var current = await scoped.Where(m => m.Id == input.Id).Select(m => new { m.Id, m.AgencyId, m.OccurredAt, m.Status }).FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound("Mouvement de petite caisse introuvable.");
        if (current.Status != PettyCashStatus.DRAFT) throw BusinessException.Conflict("Cette dépense a déjà été traitée.", new { reason = "petty-cash-not-draft" });
        var status = input.Validate ? PettyCashStatus.VALIDATED : PettyCashStatus.REFUSED;
        var reason = input.Validate || string.IsNullOrEmpty(input.Reason) ? null : input.Reason;
        var userId = caller.UserId;
        await PettyCashDays.WriteAsync(db, clock, [(current.AgencyId, PettyCashDays.DayOf(current.OccurredAt))], async () =>
            await db.PettyCashMovements.Where(m => m.Id == current.Id).ExecuteUpdateAsync(set => set.SetProperty(m => m.Status, status).SetProperty(m => m.RefusalReason, reason)
                .SetProperty(m => m.DecidedById, userId).SetProperty(m => m.DecidedAt, DateTime.UtcNow).SetProperty(m => m.UpdatedAt, DateTime.UtcNow), cancellationToken), cancellationToken);
        return await PettyCashMovements.OneAsync(db, scoped, input.Id, "Mouvement de petite caisse introuvable.", cancellationToken);
    }
}
