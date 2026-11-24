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

/// <summary>`confirmParcelsInBulk`, partagé avec la tâche de masse `route-outcomes`.</summary>
public static class RouteBulkOutcomes
{
    public static async Task<object> RunAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, BulkJobQueue? jobs, ConfirmRouteOutcomesCommand input, CancellationToken cancellationToken)
    {
        var statusId = RouteInputs.Int(input.StatusId)!.Value;
        var route = await RouteConfirmation.RouteAsync(db, caller, input.Code, cancellationToken);
        var ids = input.ParcelIds!.Distinct().ToList();
        var links = await db.RouteParcels.Include(l => l.Parcel).Where(l => l.RouteId == route.Id && ids.Contains(l.ParcelId)).ToListAsync(cancellationToken);
        if (links.Count != ids.Count)
        {
            throw BusinessException.Validation($"{ids.Count - links.Count} colis coché(s) n’appartiennent plus au circuit {input.Code} : rechargez.",
                new { reason = "parcel-not-on-route", count = ids.Count - links.Count });
        }
        if (statusId == RouteStatusIds.Delivered)
        {
            var byCheque = links.Where(l => l.Parcel.ExpectedPayment == ExpectedPayment.CHEQUE).Select(l => l.Parcel.Code).ToList();
            if (byCheque.Count > 0)
            {
                throw BusinessException.Validation($"{byCheque.Count} colis attendus en chèque : livrez-les un par un ({string.Join(", ", byCheque.Take(5))}).",
                    new { reason = "bulk-cheque-parcels", count = byCheque.Count, codes = byCheque.Take(50).ToList() });
            }
        }
        if (!input.FromJob)
        {
            if (input.Async && jobs is not null && ids.Count > NoteRules.BulkAsyncThreshold)
            {
                return new BulkJobAcceptedDto(await BulkJobWrites.CreateAsync(db, caller, jobs, BulkJobKinds.RouteOutcomes, input.Code,
                    new Dictionary<string, object?> { ["statusId"] = statusId, ["scheduledAt"] = input.ScheduledAt, ["comment"] = input.Comment }, ids, cancellationToken));
            }
            await BulkJobWrites.AssertIdleAsync(db, input.Code, cancellationToken);
        }
        var todo = links.Where(l => !(l.Confirmed && l.Parcel.StatusId == statusId)).ToList();
        var before = todo.Select(l => (l.Parcel.StatusId, l.Parcel.ClientId, l.Parcel.Code)).ToList();
        long cashExpected = 0;
        var outcome = new RouteOutcome(true, statusId, input.ScheduledAt, input.Comment);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            foreach (var link in todo)
            {
                if (statusId == RouteStatusIds.Delivered)
                {
                    cashExpected += Billing.CodToCollect(Money.ToCents(link.Parcel.CodAmount), Money.ToCents(link.Parcel.DeliveryFee), link.Parcel.FeeIncluded, link.Parcel.AlreadyPaid);
                }
                await RouteConfirmation.ApplyAsync(db, caller, route, link, outcome, false, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        db.ChangeTracker.Clear();
        await RouteClosing.AfterOutcomeAsync(db, caller, logger, route, cancellationToken);
        foreach (var (from, clientId, code) in before.Where(b => b.StatusId != statusId))
        {
            await ParcelStatusChange.NotifyMerchantOfStatusAsync(db, logger, clientId, code, statusId, string.IsNullOrEmpty(input.ScheduledAt) ? null : input.ScheduledAt, cancellationToken);
        }
        return new RouteBulkConfirmResultDto(todo.Count, cashExpected);
    }
}
