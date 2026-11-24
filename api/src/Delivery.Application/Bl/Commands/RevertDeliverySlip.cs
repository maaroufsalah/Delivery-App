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

/// <summary>`deliverySlipRevertSchema` : l'administrateur fait reculer un BL d'une étape.</summary>
public sealed record RevertDeliverySlipCommand : IRequest<DeliverySlipListItemDto>
{
    [JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public string? Reason { get; init => field = Zod.Trim(value); }
}

public sealed class RevertDeliverySlipValidator : ZodValidator<RevertDeliverySlipCommand>
{
    public RevertDeliverySlipValidator()
    {
        RuleFor(x => x.Reason).Required().MinLength(1, "required").MaxLength(255);
    }
}

internal sealed class RevertDeliverySlipHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RevertDeliverySlipCommand, DeliverySlipListItemDto>
{
    public async Task<DeliverySlipListItemDto> Handle(RevertDeliverySlipCommand input, CancellationToken cancellationToken)
    {
        if (!caller.IsSuperAdmin()) throw BusinessException.Forbidden("Seul un administrateur revient sur l’état d’un BL.");
        var slip = await BlRows.Slips(db, caller).Where(s => s.Id == input.Id).Select(s => new
        {
            s.Id, s.Status, s.ParcelId, s.Parcel.StatusId, Carrier = s.NoteItems.Where(i => i.Note.Status != BlNoteStatus.DRAFT).Select(i => i.Note.Code).FirstOrDefault(),
        }).FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound("BL introuvable.");
        if (slip.Carrier is not null) throw BusinessException.Conflict($"Ce BL est porté par le bon {slip.Carrier} : il suit son bon.", new { reason = "bl-on-note", code = slip.Carrier });
        DeliverySlipStatus back = slip.Status switch
        {
            DeliverySlipStatus.RETURNED => DeliverySlipStatus.RECEIVED,
            DeliverySlipStatus.RECEIVED => DeliverySlipStatus.COLLECTED,
            _ => throw BusinessException.Conflict("Ce BL est à sa première étape : il n’y a rien à défaire.", new { reason = "bl-revert-impossible" }),
        };
        var now = DateTime.UtcNow;
        var comment = $"Retour arrière : {input.Reason}";
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var from = slip.Status;
            var count = back == DeliverySlipStatus.RECEIVED
                ? await db.DeliverySlips.Where(s => s.Id == slip.Id && s.Status == from).ExecuteUpdateAsync(set => set.SetProperty(s => s.Status, back)
                    .SetProperty(s => s.ReturnedAt, (DateTime?)null).SetProperty(s => s.ReturnedById, (string?)null), cancellationToken)
                : await db.DeliverySlips.Where(s => s.Id == slip.Id && s.Status == from).ExecuteUpdateAsync(set => set.SetProperty(s => s.Status, back)
                    .SetProperty(s => s.ReceivedAt, (DateTime?)null).SetProperty(s => s.ReceivedById, (string?)null).SetProperty(s => s.HandedOverAt, (DateTime?)null)
                    .SetProperty(s => s.HandedOverById, (string?)null), cancellationToken);
            if (count == 0) throw BusinessException.Conflict("Ce BL a changé d’état entre-temps : rechargez.", new { reason = "bl-moved" });
            db.DeliverySlipEvents.Add(new DeliverySlipEvent { SlipId = slip.Id, Status = back, Comment = comment, AuthorId = caller.UserId, CreatedAt = now });
            db.ParcelEvents.Add(new ParcelEvent { ParcelId = slip.ParcelId, StatusId = slip.StatusId, Comment = $"BL — {comment}", AuthorId = caller.UserId, CreatedAt = now });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await BlRows.Project(db.DeliverySlips.AsNoTracking().Where(s => s.Id == slip.Id)).FirstAsync(cancellationToken);
    }
}
