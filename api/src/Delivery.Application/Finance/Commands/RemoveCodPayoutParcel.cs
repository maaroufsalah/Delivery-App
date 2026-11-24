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

/// <summary>`DELETE /cod-payouts/:code/parcels/:parcelId`.</summary>
public sealed record RemoveCodPayoutParcelCommand(string Code, string ParcelId) : IRequest<CodPayoutDetailDto>;

internal sealed class RemoveCodPayoutParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveCodPayoutParcelCommand, CodPayoutDetailDto>
{
    public async Task<CodPayoutDetailDto> Handle(RemoveCodPayoutParcelCommand request, CancellationToken cancellationToken)
    {
        var payout = await CodPayouts.Scoped(db.CodPayouts.AsNoTracking(), caller).Where(p => p.Code == request.Code).Select(p => new { p.Id, p.Status }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound($"Bon de versement introuvable : {request.Code}");
        if (payout.Status != CodPayoutStatus.DRAFT) throw BusinessException.Conflict("Ce bon est versé : il ne se modifie plus.", new { reason = "payout-paid" });
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.CodPayoutParcels.Where(l => l.PayoutId == payout.Id && l.ParcelId == request.ParcelId).ExecuteDeleteAsync(cancellationToken);
            await CodPayouts.RefreshTotalAsync(db, payout.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await CodPayouts.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}
