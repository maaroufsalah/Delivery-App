using System.Globalization;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Returns;

/// <summary>`returnHandoverCancelSchema` : le bon annulé relâche ses colis.</summary>
public sealed record CancelHandoverCommand : IRequest<ReturnHandoverDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? Reason { get; init => field = Zod.Trim(value); }
}

public sealed class CancelHandoverValidator : ZodValidator<CancelHandoverCommand>
{
    public CancelHandoverValidator()
    {
        RuleFor(x => x.Reason).MaxLength(500);
    }
}

internal sealed class CancelHandoverHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CancelHandoverCommand, ReturnHandoverDetailDto>
{
    public async Task<ReturnHandoverDetailDto> Handle(CancelHandoverCommand input, CancellationToken cancellationToken)
    {
        var handover = await ReturnRows.Handovers(db.ReturnHandovers.AsNoTracking(), caller).Where(h => h.Code == input.Code).Select(h => new { h.Id, h.Status }).FirstOrDefaultAsync(cancellationToken)
                       ?? throw BusinessException.NotFound($"Bon de retour introuvable : {input.Code}");
        var status = Money.ToWire(handover.Status);
        if (!ReturnRows.IsCancellable(status))
        {
            var cancelled = status == "cancelled";
            throw BusinessException.Conflict(cancelled ? "Ce bon est déjà annulé." : "La réception de ce bon est confirmée : il ne s’annule plus.",
                new { reason = cancelled ? "handover-cancelled" : "handover-already-confirmed" });
        }
        var now = DateTime.UtcNow;
        var reason = ReturnRows.Text(input.Reason);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.ReturnHandoverItems.Where(i => i.HandoverId == handover.Id).ExecuteUpdateAsync(set => set.SetProperty(i => i.ReservedParcelId, (string?)null), cancellationToken);
            await db.ReturnHandovers.Where(h => h.Id == handover.Id).ExecuteUpdateAsync(set => set.SetProperty(h => h.Status, ReturnHandoverStatus.CANCELLED).SetProperty(h => h.CancelledAt, now)
                .SetProperty(h => h.CancelledById, caller.UserId).SetProperty(h => h.CancelReason, reason).SetProperty(h => h.UpdatedAt, now), cancellationToken);
            db.ReturnHandoverEvents.Add(new ReturnHandoverEvent
            {
                HandoverId = handover.Id, Status = ReturnHandoverStatus.CANCELLED, Comment = reason is null ? "Bon annulé" : $"Bon annulé : {reason}", AuthorId = caller.UserId, CreatedAt = now,
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await Handovers.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
