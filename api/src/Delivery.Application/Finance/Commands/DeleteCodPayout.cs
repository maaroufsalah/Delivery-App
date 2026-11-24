using System.Globalization;
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
using Delivery.Application.Parcels;
using Delivery.Application.PettyCash;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>`DELETE /cod-payouts/:code` : un bon non versé.</summary>
public sealed record DeleteCodPayoutCommand(string Code) : IRequest<ReturnCodeDto>;

internal sealed class DeleteCodPayoutHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<DeleteCodPayoutCommand, ReturnCodeDto>
{
    public async Task<ReturnCodeDto> Handle(DeleteCodPayoutCommand request, CancellationToken cancellationToken)
    {
        var payout = await CodPayouts.Scoped(db.CodPayouts.AsNoTracking(), caller).Where(p => p.Code == request.Code).Select(p => new { p.Id, p.Status }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound($"Bon de versement introuvable : {request.Code}");
        if (payout.Status == CodPayoutStatus.PAID) throw BusinessException.Conflict("Un bon de versement versé ne se supprime pas.", new { reason = "payout-paid" });
        await db.CodPayouts.Where(p => p.Id == payout.Id).ExecuteDeleteAsync(cancellationToken);
        return new ReturnCodeDto(request.Code);
    }
}
