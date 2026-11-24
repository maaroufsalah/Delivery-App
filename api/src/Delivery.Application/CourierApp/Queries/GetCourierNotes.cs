using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.CourierApp;

/// <summary>`GET /courier/me/notes` (`courierNotesQuerySchema`) : les bons qui portent des colis de ce livreur.</summary>
public sealed record GetCourierNotesQuery(string? Kind, string? Status) : IRequest<IReadOnlyList<CourierPickupNoteDto>>;

public sealed class GetCourierNotesValidator : ZodValidator<GetCourierNotesQuery>
{
    public GetCourierNotesValidator()
    {
        RuleFor(x => x.Kind).OneOf(["pickup", "dropoff"]);
        RuleFor(x => x.Status).OneOf(["pending", "done"]);
    }
}

internal sealed class GetCourierNotesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCourierNotesQuery, IReadOnlyList<CourierPickupNoteDto>>
{
    public async Task<IReadOnlyList<CourierPickupNoteDto>> Handle(GetCourierNotesQuery request, CancellationToken cancellationToken)
    {
        var courierId = caller.CourierId ?? throw BusinessException.Forbidden("Ce compte n’est pas rattaché à une fiche livreur.");
        var type = request.Kind == "dropoff" ? DeliveryNoteType.DROPOFF : DeliveryNoteType.PICKUP;
        var notes = db.DeliveryNotes.AsNoTracking().Where(n => n.Type == type && n.Parcels.Any(l => l.Parcel.CourierId == courierId && l.Parcel.DeletedAt == null));
        var open = request.Status != "done" ? await Of(notes.Where(n => n.Status != DeliveryNoteStatus.VALIDATED), courierId, NoteRules.SelectionMax, cancellationToken) : [];
        var done = request.Status != "pending" ? await Of(notes.Where(n => n.Status == DeliveryNoteStatus.VALIDATED), courierId, 100, cancellationToken) : [];
        return [.. open, .. done];
    }

    private static Task<List<CourierPickupNoteDto>> Of(IQueryable<DeliveryNote> notes, string courierId, int take, CancellationToken cancellationToken) =>
        notes.OrderByDescending(n => n.CreatedAt).Take(take).Select(n => new CourierPickupNoteDto(n.Id, n.Code, n.Type == DeliveryNoteType.PICKUP ? "pickup" : "dropoff",
            n.Status == DeliveryNoteStatus.AWAITING_VALIDATION ? "awaiting-validation" : n.Status == DeliveryNoteStatus.PICKED_UP_BY_COURIER ? "picked-up-by-courier" : "validated",
            n.Client.Name, n.Client.Phone, n.Client.Address ?? string.Empty, n.LocationCity.Name,
            n.Parcels.Count(l => l.Parcel.CourierId == courierId && l.Parcel.DeletedAt == null),
            n.Parcels.Count(l => l.Parcel.CourierId == courierId && l.Parcel.DeletedAt == null && l.Parcel.StatusId != NoteRules.AwaitingPickup && l.Parcel.StatusId != NoteRules.PickupInProgress),
            n.RemovedParcels.Count(r => r.Parcel.CourierId == courierId && r.RestoredAt == null), n.CreatedAt, n.ValidatedAt)).ToListAsync(cancellationToken);
}
