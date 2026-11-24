using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.CourierApp;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using Delivery.Domain.Pricing;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Returns;

/// <summary>`DELETE /return-slips/:code/parcels/:parcelId`.</summary>
public sealed record RemoveSlipParcelCommand(string Code, string ParcelId) : IRequest<ReturnSlipDetailDto>;

internal sealed class RemoveSlipParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveSlipParcelCommand, ReturnSlipDetailDto>
{
    public async Task<ReturnSlipDetailDto> Handle(RemoveSlipParcelCommand request, CancellationToken cancellationToken)
    {
        var slip = await ReturnRows.Slips(db.ReturnSlips.AsNoTracking(), caller).Where(s => s.Code == request.Code).Select(s => new { s.Id, s.Status }).FirstOrDefaultAsync(cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon de retour introuvable : {request.Code}");
        if (slip.Status != ReturnSlipStatus.DRAFT) throw BusinessException.Conflict("Ce bon est remis : il ne se modifie plus.");
        await db.ReturnSlipItems.Where(i => i.SlipId == slip.Id && i.ParcelId == request.ParcelId).ExecuteDeleteAsync(cancellationToken);
        return await Slips.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}
