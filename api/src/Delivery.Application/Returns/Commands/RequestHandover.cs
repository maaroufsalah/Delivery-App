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

/// <summary>`POST /return-handovers/:code/request` : le brouillon devient une demande au livreur.</summary>
public sealed record RequestHandoverCommand(string Code) : IRequest<ReturnHandoverDetailDto>;

internal sealed class RequestHandoverHandler(IAppDbContext db, ICurrentUser caller, ILogger<RequestHandoverHandler> logger) : IRequestHandler<RequestHandoverCommand, ReturnHandoverDetailDto>
{
    public async Task<ReturnHandoverDetailDto> Handle(RequestHandoverCommand request, CancellationToken cancellationToken)
    {
        var handover = await ReturnRows.Handovers(db.ReturnHandovers.AsNoTracking(), caller).Where(h => h.Code == request.Code)
                           .Select(h => new { h.Id, h.Status, Items = h.Items.Count }).FirstOrDefaultAsync(cancellationToken)
                       ?? throw BusinessException.NotFound($"Bon de retour introuvable : {request.Code}");
        if (handover.Status != ReturnHandoverStatus.DRAFT) throw BusinessException.Conflict("Ce bon n’est plus un brouillon.", new { reason = "handover-not-draft" });
        if (handover.Items == 0) throw BusinessException.Validation("Un bon de retour sans colis ne se demande pas.", new { reason = "handover-empty" });
        await Handovers.AdvanceAsync(db, caller, handover.Id, ReturnHandoverStatus.RETURN_REQUESTED, "Retour demandé au livreur", DateTime.UtcNow, cancellationToken);
        db.ChangeTracker.Clear();
        var detail = await Handovers.DetailAsync(db, caller, request.Code, cancellationToken);
        await Handovers.NotifyCourierAsync(db, logger, detail.CourierId, "return-note-requested", detail, cancellationToken);
        return detail;
    }
}
