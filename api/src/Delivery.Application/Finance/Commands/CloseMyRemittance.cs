using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Finance;

/// <summary>`POST /courier/me/remittance/close` : le livreur ferme sa caisse ; l'agence comptera et confirmera.</summary>
public sealed record CloseMyRemittanceCommand : IRequest<CourierOpenRemittanceDto>;

internal sealed class CloseMyRemittanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CloseMyRemittanceCommand, CourierOpenRemittanceDto>
{
    public async Task<CourierOpenRemittanceDto> Handle(CloseMyRemittanceCommand request, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not { } courierId) throw BusinessException.Forbidden("Seul le livreur clôture sa propre caisse.", new { reason = "closing-own-cash-only" });
        var open = await db.CashRemittances.AsNoTracking().Where(r => r.CourierId == courierId && r.Status == CashRemittanceStatus.DRAFT && r.CourierClosedAt == null)
                       .Select(r => new { r.Id, r.Code, Lines = r.Parcels.Count }).FirstOrDefaultAsync(cancellationToken)
                   ?? throw BusinessException.Conflict("Aucune caisse ouverte à clôturer.", new { reason = "no-open-remittance" });
        if (open.Lines == 0) throw BusinessException.Validation("Une caisse vide ne se clôture pas.", new { reason = "remittance-empty" });
        var userId = caller.UserId;
        var closed = await db.CashRemittances.Where(r => r.Id == open.Id && r.Status == CashRemittanceStatus.DRAFT && r.CourierClosedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.CourierClosedAt, DateTime.UtcNow).SetProperty(r => r.CourierClosedById, userId), cancellationToken);
        if (closed == 0) throw BusinessException.Conflict("Cette caisse vient d’être clôturée.", new { reason = "remittance-already-closed" });
        return new CourierOpenRemittanceDto(await Remittances.DetailAsync(db, caller, open.Code, cancellationToken));
    }
}
