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

/// <summary>`POST /routes/:code/dispatch` : au-delà de 300 colis avec `x-bulk-jobs: 1`, une tâche de masse.</summary>
public sealed record DispatchRouteCommand(string Code, bool Async) : IRequest<object>;

internal sealed class DispatchRouteHandler(IAppDbContext db, ICurrentUser caller, ILogger<DispatchRouteHandler> logger, BulkJobQueue jobs) : IRequestHandler<DispatchRouteCommand, object>
{
    public async Task<object> Handle(DispatchRouteCommand request, CancellationToken cancellationToken)
    {
        var route = await RouteRows.Scoped(db.Routes.AsNoTracking(), caller).Where(r => r.Code == request.Code)
            .Select(r => new { r.Code, r.Status, Parcels = r.Parcels.Select(l => l.ParcelId).ToList() }).FirstOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound($"Circuit introuvable : {request.Code}");
        if (route.Status != RouteStatus.DRAFT) throw BusinessException.Conflict("Ce circuit est déjà parti.", new { reason = "route-dispatched" });
        if (route.Parcels.Count == 0) throw BusinessException.Validation("Un circuit sans colis ne part pas.");
        if (request.Async && route.Parcels.Count > NoteRules.BulkAsyncThreshold)
        {
            return new BulkJobAcceptedDto(await BulkJobWrites.CreateAsync(db, caller, jobs, BulkJobKinds.RouteDispatch, route.Code, new Dictionary<string, object?>(), route.Parcels, cancellationToken));
        }
        await BulkJobWrites.AssertIdleAsync(db, route.Code, cancellationToken);
        await RouteDispatch.RunAsync(db, caller, logger, request.Code, new RouteDispatch.Options(), cancellationToken);
        return await RouteRows.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}
