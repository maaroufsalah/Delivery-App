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

/// <summary>`POST /return-handovers/:code/confirm` : l'agence reçoit ; chaque colis passe « Retourné à l'agence », en une transaction.</summary>
public sealed record ConfirmHandoverCommand(string Code) : IRequest<ReturnHandoverDetailDto>;

internal sealed class ConfirmHandoverHandler(IAppDbContext db, ICurrentUser caller, ILogger<ConfirmHandoverHandler> logger) : IRequestHandler<ConfirmHandoverCommand, ReturnHandoverDetailDto>
{
    public async Task<ReturnHandoverDetailDto> Handle(ConfirmHandoverCommand request, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not null) throw BusinessException.Forbidden("Seule l’agence confirme la réception d’un bon de retour.", new { reason = "receipt-is-agency-only" });
        var handover = await ReturnRows.Handovers(db.ReturnHandovers.AsNoTracking(), caller).Where(h => h.Code == request.Code)
                           .Select(h => new { h.Id, h.Code, h.Status, h.AgencyId, h.CourierId, Items = h.Items.Select(i => new { i.Id, i.ParcelId, i.Parcel.StatusId }).ToList() })
                           .FirstOrDefaultAsync(cancellationToken)
                       ?? throw BusinessException.NotFound($"Bon de retour introuvable : {request.Code}");
        if (handover.Status is ReturnHandoverStatus.CONFIRMED or ReturnHandoverStatus.CLOSED)
        {
            throw BusinessException.Conflict("La réception de ce bon est déjà confirmée.", new { reason = "handover-already-confirmed" });
        }
        if (handover.Status == ReturnHandoverStatus.CANCELLED) throw BusinessException.Conflict("Ce bon est annulé.", new { reason = "handover-cancelled" });
        if (handover.Items.Count == 0) throw BusinessException.Validation("Un bon sans colis ne se confirme pas.", new { reason = "handover-empty" });
        await ReturnRows.AssertAgencyReachableAsync(db, caller, handover.AgencyId, cancellationToken);
        var now = DateTime.UtcNow;
        var shortName = await ReturnRows.ShortNameAsync(db, cancellationToken);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            foreach (var item in handover.Items)
            {
                if (!StatusCatalog.IsReturnEligible(item.StatusId))
                {
                    throw BusinessException.Conflict($"Le colis {item.ParcelId} a changé de statut depuis son ajout.", new { reason = "parcel-moved", parcelId = item.ParcelId });
                }
                await db.Parcels.Where(p => p.Id == item.ParcelId).ExecuteUpdateAsync(set => set.SetProperty(p => p.StatusId, StatusCatalog.ReturnedToAgency).SetProperty(p => p.StatusAt, now)
                    .SetProperty(p => p.UpdatedAt, now), cancellationToken);
                db.ParcelEvents.Add(new ParcelEvent { ParcelId = item.ParcelId, StatusId = StatusCatalog.ReturnedToAgency, Comment = $"Bon de retour {handover.Code}", AuthorId = caller.UserId, CreatedAt = now });
                await db.ReturnHandoverItems.Where(i => i.Id == item.Id).ExecuteUpdateAsync(set => set.SetProperty(i => i.ConfirmedParcelId, item.ParcelId), cancellationToken);
            }
            await db.ReturnHandovers.Where(h => h.Id == handover.Id).ExecuteUpdateAsync(set => set.SetProperty(h => h.Status, ReturnHandoverStatus.CONFIRMED).SetProperty(h => h.ConfirmedAt, now)
                .SetProperty(h => h.ConfirmedById, caller.UserId).SetProperty(h => h.UpdatedAt, now), cancellationToken);
            db.ReturnHandoverEvents.Add(new ReturnHandoverEvent { HandoverId = handover.Id, Status = ReturnHandoverStatus.CONFIRMED, Comment = $"Réception confirmée par {shortName}", AuthorId = caller.UserId, CreatedAt = now });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        // `autoCloseRoutesOf` : un circuit de livraison dont le dernier colis rentre se clôt seul.
        var parcelIds = handover.Items.Select(i => i.ParcelId).ToList();
        var routeIds = await db.RouteParcels.AsNoTracking()
            .Where(l => parcelIds.Contains(l.ParcelId) && l.Route.Kind == RouteKind.DELIVERY && (l.Route.Status == RouteStatus.DISPATCHED || l.Route.Status == RouteStatus.DELIVERING))
            .Select(l => l.RouteId).Distinct().ToListAsync(cancellationToken);
        foreach (var routeId in routeIds) await RouteClosing.AutoCloseDeliveryAsync(db, caller, logger, routeId, cancellationToken);
        db.ChangeTracker.Clear();
        var detail = await Handovers.DetailAsync(db, caller, request.Code, cancellationToken);
        await Handovers.NotifyCourierAsync(db, logger, detail.CourierId, "return-note-received", detail, cancellationToken);
        return detail;
    }
}
