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

/// <summary>`DELETE /return-handovers/:code/parcels/:parcelId`.</summary>
public sealed record RemoveHandoverParcelCommand(string Code, string ParcelId) : IRequest<ReturnHandoverDetailDto>;

internal sealed class RemoveHandoverParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveHandoverParcelCommand, ReturnHandoverDetailDto>
{
    public async Task<ReturnHandoverDetailDto> Handle(RemoveHandoverParcelCommand request, CancellationToken cancellationToken)
    {
        var handover = await Handovers.LoadEditableAsync(db, caller, request.Code, cancellationToken);
        await db.ReturnHandoverItems.Where(i => i.HandoverId == handover.Id && i.ParcelId == request.ParcelId).ExecuteDeleteAsync(cancellationToken);
        return await Handovers.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}
