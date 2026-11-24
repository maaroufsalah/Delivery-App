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

/// <summary>`DELETE /return-handovers/:code` : un brouillon seulement ; les autres s'annulent.</summary>
public sealed record DeleteHandoverCommand(string Code) : IRequest<ReturnCodeDto>;

internal sealed class DeleteHandoverHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<DeleteHandoverCommand, ReturnCodeDto>
{
    public async Task<ReturnCodeDto> Handle(DeleteHandoverCommand request, CancellationToken cancellationToken)
    {
        var handover = await ReturnRows.Handovers(db.ReturnHandovers.AsNoTracking(), caller).Where(h => h.Code == request.Code).Select(h => new { h.Id, h.Status }).FirstOrDefaultAsync(cancellationToken)
                       ?? throw BusinessException.NotFound($"Bon de retour introuvable : {request.Code}");
        if (handover.Status != ReturnHandoverStatus.DRAFT) throw BusinessException.Conflict("Seul un brouillon se supprime ; les autres bons s’annulent.", new { reason = "handover-not-draft" });
        await db.ReturnHandovers.Where(h => h.Id == handover.Id).ExecuteDeleteAsync(cancellationToken);
        return new ReturnCodeDto(request.Code);
    }
}
