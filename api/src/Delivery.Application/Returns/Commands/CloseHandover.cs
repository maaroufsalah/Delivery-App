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

/// <summary>`POST /return-handovers/:code/close`.</summary>
public sealed record CloseHandoverCommand(string Code) : IRequest<ReturnHandoverDetailDto>;

internal sealed class CloseHandoverHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CloseHandoverCommand, ReturnHandoverDetailDto>
{
    public async Task<ReturnHandoverDetailDto> Handle(CloseHandoverCommand request, CancellationToken cancellationToken)
    {
        var handover = await ReturnRows.Handovers(db.ReturnHandovers.AsNoTracking(), caller).Where(h => h.Code == request.Code).Select(h => new { h.Id, h.Status }).FirstOrDefaultAsync(cancellationToken)
                       ?? throw BusinessException.NotFound($"Bon de retour introuvable : {request.Code}");
        if (handover.Status != ReturnHandoverStatus.CONFIRMED) throw BusinessException.Conflict("Seul un bon dont la réception est confirmée se clôture.", new { reason = "handover-not-confirmed" });
        await Handovers.AdvanceAsync(db, caller, handover.Id, ReturnHandoverStatus.CLOSED, "Bon clôturé", DateTime.UtcNow, cancellationToken);
        db.ChangeTracker.Clear();
        return await Handovers.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}
