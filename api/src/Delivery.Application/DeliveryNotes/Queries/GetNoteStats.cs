using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.DeliveryNotes;

/// <summary>`GET /delivery-notes/stats` : bons, en attente, colis, ramassages offerts, conditions de ramassage.</summary>
public sealed record GetNoteStatsQuery(NoteListFilter Filter) : IRequest<DeliveryNoteStatsDto>;

internal sealed class GetNoteStatsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetNoteStatsQuery, DeliveryNoteStatsDto>
{
    public async Task<DeliveryNoteStatsDto> Handle(GetNoteStatsQuery request, CancellationToken cancellationToken)
    {
        var where = NoteRows.Filter(db.DeliveryNotes.AsNoTracking(), caller, request.Filter);
        var policy = await NoteRules.PolicyAsync(db, caller.AgencyId, cancellationToken);
        var total = await where.CountAsync(cancellationToken);
        var awaiting = await where.CountAsync(n => n.Status != DeliveryNoteStatus.VALIDATED, cancellationToken);
        var parcels = await where.SelectMany(n => n.Parcels).CountAsync(cancellationToken);
        var pickups = await where.Where(n => n.Type == DeliveryNoteType.PICKUP)
            .Select(n => new { n.Status, n.PickupFeeApplied, n.PickupThresholdApplied, n.Client.PickupFee, n.Client.FreePickupThreshold, Count = n.Parcels.Count }).ToListAsync(cancellationToken);
        var waived = pickups.Count(n => n.Count >= NoteRules.RuleOf(n.Status, n.PickupFeeApplied, n.PickupThresholdApplied, n.PickupFee, n.FreePickupThreshold, policy).FreeFrom);
        var rule = policy;
        if (caller.ClientId is { } clientId)
        {
            var client = await db.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => new { c.PickupFee, c.FreePickupThreshold }).FirstOrDefaultAsync(cancellationToken);
            rule = NoteRules.EffectivePickupPolicy(policy, client?.PickupFee, client?.FreePickupThreshold);
        }
        return new DeliveryNoteStatsDto(total, awaiting, parcels, waived, rule.PickupFee, rule.FreePickupThreshold);
    }
}
