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

/// <summary>`DELETE /remittances/:code/parcels/:parcelId`.</summary>
public sealed record RemoveRemittanceParcelCommand(string Code, string ParcelId) : IRequest<RemittanceDetailDto>;

internal sealed class RemoveRemittanceParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveRemittanceParcelCommand, RemittanceDetailDto>
{
    public async Task<RemittanceDetailDto> Handle(RemoveRemittanceParcelCommand request, CancellationToken cancellationToken)
    {
        var remittance = await Remittances.RequireDraftAsync(db, caller, request.Code, cancellationToken);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.CashRemittanceParcels.Where(l => l.RemittanceId == remittance.Id && l.ParcelId == request.ParcelId).ExecuteDeleteAsync(cancellationToken);
            await RemittanceLines.RefreshTotalAsync(db, remittance.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await Remittances.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}
