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

/// <summary>`DELETE /remittances/:code` : tout bon non confirmé.</summary>
public sealed record DeleteRemittanceCommand(string Code) : IRequest<ReturnCodeDto>;

internal sealed class DeleteRemittanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<DeleteRemittanceCommand, ReturnCodeDto>
{
    public async Task<ReturnCodeDto> Handle(DeleteRemittanceCommand request, CancellationToken cancellationToken)
    {
        var remittance = await Remittances.Scoped(db.CashRemittances.AsNoTracking(), caller).Where(r => r.Code == request.Code).Select(r => new { r.Id, r.Status }).FirstOrDefaultAsync(cancellationToken)
                         ?? throw BusinessException.NotFound($"Retour de fonds introuvable : {request.Code}");
        if (remittance.Status == CashRemittanceStatus.CONFIRMED) throw BusinessException.Conflict("Un retour de fonds confirmé ne se supprime pas.", new { reason = "remittance-confirmed" });
        await db.CashRemittances.Where(r => r.Id == remittance.Id).ExecuteDeleteAsync(cancellationToken);
        return new ReturnCodeDto(request.Code);
    }
}
