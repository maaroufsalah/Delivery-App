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

/// <summary>`POST /return-handovers/:code/handover` : remis à l'entrepôt, l'agence est prévenue.</summary>
public sealed record HandOverHandoverCommand(string Code) : IRequest<ReturnHandoverDetailDto>;

internal sealed class HandOverHandoverHandler(IAppDbContext db, ICurrentUser caller, ILogger<HandOverHandoverHandler> logger) : IRequestHandler<HandOverHandoverCommand, ReturnHandoverDetailDto>
{
    public async Task<ReturnHandoverDetailDto> Handle(HandOverHandoverCommand request, CancellationToken cancellationToken)
    {
        var handover = await ReturnRows.Handovers(db.ReturnHandovers.AsNoTracking(), caller).Where(h => h.Code == request.Code).Select(h => new { h.Id, h.Status }).FirstOrDefaultAsync(cancellationToken)
                       ?? throw BusinessException.NotFound($"Bon de retour introuvable : {request.Code}");
        if (handover.Status != ReturnHandoverStatus.PICKED_UP)
        {
            throw BusinessException.Conflict("Ce bon doit être pris en charge avant d’être remis à l’entrepôt.", new { reason = "handover-not-picked-up" });
        }
        await Handovers.AdvanceAsync(db, caller, handover.Id, ReturnHandoverStatus.AWAITING_RECEIPT, "Remis à l’entrepôt, en attente de réception", DateTime.UtcNow, cancellationToken);
        db.ChangeTracker.Clear();
        var detail = await Handovers.DetailAsync(db, caller, request.Code, cancellationToken);
        try
        {
            await Notifier.NotifyStaffAsync(db, detail.AgencyId, "return-note-awaiting", "returns.confirm", new Dictionary<string, object> { ["code"] = detail.Code, ["count"] = detail.ParcelCount },
                new Dictionary<string, string> { ["topic"] = "return-note-awaiting", ["handoverCode"] = detail.Code }, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Notification « return-note-awaiting » non envoyée : {Message}", error.Message);
        }
        return detail;
    }
}
