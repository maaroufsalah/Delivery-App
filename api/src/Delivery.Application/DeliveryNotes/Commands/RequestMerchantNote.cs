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

/// <summary>`POST /merchant/me/pickups` et `/dropoffs` (`pickupRequestSchema`) : le vendeur ouvre son bon.</summary>
public sealed record RequestMerchantNoteCommand : ParcelSelection, IRequest<DeliveryNoteDetailDto>
{
    [JsonIgnore]
    public DeliveryNoteType Kind { get; init; } = DeliveryNoteType.PICKUP;
}

public sealed class RequestMerchantNoteValidator : ParcelSelectionValidator<RequestMerchantNoteCommand>;

internal sealed class RequestMerchantNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RequestMerchantNoteCommand, DeliveryNoteDetailDto>
{
    public async Task<DeliveryNoteDetailDto> Handle(RequestMerchantNoteCommand input, CancellationToken cancellationToken)
    {
        var clientId = caller.ClientId ?? throw BusinessException.Forbidden("Cet espace est réservé aux e-commerçants.", new { reason = "not-a-merchant" });
        var ids = await NoteRules.ResolveSelectionAsync(input.ParcelIds, input.All, input.Exclude, take => NoteRules.CandidateIdsAsync(db, clientId, input.Search, take, cancellationToken));
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => new { c.AgencyId, c.CityId }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound("Fiche client introuvable.");
        var agencyId = client.AgencyId ?? await db.Agencies.AsNoTracking().Where(a => a.DeletedAt == null).OrderBy(a => a.CreatedAt).Select(a => a.Id).FirstOrDefaultAsync(cancellationToken)
                       ?? throw BusinessException.Validation("Aucune agence disponible.");
        var candidates = await db.Parcels.AsNoTracking()
            .Where(p => p.ClientId == clientId && ids.Contains(p.Id) && NoteRules.CandidateStatusIds.Contains(p.StatusId) && p.DeletedAt == null).Select(p => p.Id).ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            var pickup = input.Kind == DeliveryNoteType.PICKUP;
            throw BusinessException.Validation(pickup ? "Aucun colis à faire ramasser dans cette sélection." : "Aucun colis à déposer dans cette sélection.",
                new { reason = pickup ? "no-pickup-candidate" : "no-dropoff-candidate" });
        }
        var code = await NoteCreation.CreateAsync(db, caller, input.Kind, clientId, agencyId, client.CityId, candidates, cancellationToken);
        return await NoteRows.DetailAsync(db, caller, code, cancellationToken);
    }
}
