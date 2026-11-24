using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.BulkJobs;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>`confirmParcel` sans la fiche : partagé avec le statut de terrain de l'administrateur.</summary>
public static class RouteParcelConfirm
{
    public static async Task RunAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, string code, string parcelId, RouteOutcome outcome, bool adminFieldRevision, CancellationToken cancellationToken)
    {
        var route = await RouteConfirmation.RouteAsync(db, caller, code, cancellationToken);
        var link = await db.RouteParcels.Include(l => l.Parcel).FirstOrDefaultAsync(l => l.RouteId == route.Id && l.ParcelId == parcelId, cancellationToken)
                   ?? throw BusinessException.Validation($"Ce colis n'appartient pas au circuit {code}.");
        var (from, clientId, parcelCode) = (link.Parcel.StatusId, link.Parcel.ClientId, link.Parcel.Code);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await RouteConfirmation.ApplyAsync(db, caller, route, link, outcome, adminFieldRevision, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        db.ChangeTracker.Clear();
        await RouteClosing.AfterOutcomeAsync(db, caller, logger, route, cancellationToken);
        if (outcome.StatusId is { } to && to != from)
        {
            await ParcelStatusChange.NotifyMerchantOfStatusAsync(db, logger, clientId, parcelCode, to, string.IsNullOrEmpty(outcome.ScheduledAt) ? null : outcome.ScheduledAt, cancellationToken);
        }
    }
}
