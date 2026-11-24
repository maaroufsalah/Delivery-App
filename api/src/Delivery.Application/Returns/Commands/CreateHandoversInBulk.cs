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

/// <summary>`POST /return-handovers/bulk` : un bon par livreur et par agence ; verrou en base, donc idempotent.</summary>
public sealed record CreateHandoversInBulkCommand : IRequest<ReturnBulkSummaryDto>;

internal sealed class CreateHandoversInBulkHandler(IAppDbContext db, ICurrentUser caller, ILogger<CreateHandoversInBulkHandler> logger) : IRequestHandler<CreateHandoversInBulkCommand, ReturnBulkSummaryDto>
{
    public async Task<ReturnBulkSummaryDto> Handle(CreateHandoversInBulkCommand request, CancellationToken cancellationToken)
    {
        ReturnRows.AssertBackOffice(caller);
        var now = DateTime.UtcNow;
        var opened = new List<(string Code, string CourierId, int Count)>();
        ReturnBulkSummaryDto summary;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('return-handover-bulk'))", cancellationToken);
            var groups = await Handovers.BulkGroupsAsync(db, caller, cancellationToken);
            foreach (var group in groups)
            {
                var handover = await Handovers.OpenAsync(db, caller, group.OwnerId, group.AgencyId, ReturnHandoverOrigin.ADMIN, ReturnHandoverStatus.RETURN_REQUESTED, false, null, now, cancellationToken);
                foreach (var parcelId in group.Parcels) db.ReturnHandoverItems.Add(new ReturnHandoverItem { HandoverId = handover.Id, ParcelId = parcelId, ReservedParcelId = parcelId, CreatedAt = now });
                await db.SaveChangesAsync(cancellationToken);
                opened.Add((handover.Code, group.OwnerId, group.Parcels.Count));
            }
            summary = ReturnRows.Summarize(groups, opened.Select(o => o.Code).ToList());
            await transaction.CommitAsync(cancellationToken);
        }
        foreach (var (code, courierId, count) in opened)
        {
            try
            {
                await Notifier.NotifyCourierAsync(db, courierId, "return-note-requested", new Dictionary<string, object> { ["code"] = code, ["count"] = count },
                    new Dictionary<string, string> { ["topic"] = "return-note-requested", ["handoverCode"] = code }, cancellationToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning("Notification « return-note-requested » non envoyée : {Message}", error.Message);
            }
        }
        return summary;
    }
}
