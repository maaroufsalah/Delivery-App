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

/// <summary>`DELETE /return-slips/:code` : un bon remis ne se supprime pas.</summary>
public sealed record DeleteSlipCommand(string Code) : IRequest<ReturnCodeDto>;

internal sealed class DeleteSlipHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<DeleteSlipCommand, ReturnCodeDto>
{
    public async Task<ReturnCodeDto> Handle(DeleteSlipCommand request, CancellationToken cancellationToken)
    {
        var slip = await ReturnRows.Slips(db.ReturnSlips.AsNoTracking(), caller).Where(s => s.Code == request.Code).Select(s => new { s.Id, s.Status }).FirstOrDefaultAsync(cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon de retour introuvable : {request.Code}");
        if (slip.Status == ReturnSlipStatus.DELIVERED) throw BusinessException.Conflict("Un bon remis ne se supprime pas.");
        await db.ReturnSlips.Where(s => s.Id == slip.Id).ExecuteDeleteAsync(cancellationToken);
        return new ReturnCodeDto(request.Code);
    }
}
