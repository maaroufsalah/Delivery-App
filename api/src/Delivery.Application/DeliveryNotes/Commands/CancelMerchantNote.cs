using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.DeliveryNotes;

/// <summary>`DELETE /merchant/me/notes/:code` : le vendeur annule son bon (mêmes gardes que la suppression).</summary>
public sealed record CancelMerchantNoteCommand(string Code) : IRequest<NoteCodeDto>;

internal sealed class CancelMerchantNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CancelMerchantNoteCommand, NoteCodeDto>
{
    public Task<NoteCodeDto> Handle(CancelMerchantNoteCommand request, CancellationToken cancellationToken)
    {
        if (caller.ClientId is null) throw BusinessException.Forbidden("Cet espace est réservé aux e-commerçants.", new { reason = "not-a-merchant" });
        return NoteDeletion.DeleteAsync(db, caller, request.Code, cancellationToken);
    }
}
