using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Bl;

/// <summary>`collect` / `collectOnDelivery`.</summary>
public static class BlCollection
{
    public static async Task CollectAsync(IAppDbContext db, ICurrentUser caller, string parcelCode, bool collected, string? reference, CancellationToken cancellationToken)
    {
        var parcel = await db.Parcels.AsNoTracking().Where(p => p.Code == parcelCode && p.DeletedAt == null)
                         .Select(p => new { p.Id, p.Code, p.ClientId, p.CourierId, p.BlRequested, p.StatusId, p.Client.IsPro, HasSlip = p.DeliverySlip != null }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound($"Colis introuvable : {parcelCode}");
        if (!parcel.IsPro) throw BusinessException.Conflict("Cet e-commerçant n'est pas un Client Pro.", new { reason = "client-not-pro" });
        if (!parcel.BlRequested) throw BusinessException.Conflict("Ce colis ne demande aucun BL.", new { reason = "bl-not-requested", code = parcel.Code });
        if (parcel.HasSlip) throw BusinessException.Conflict("Le BL de ce colis est déjà enregistré.", new { reason = "bl-already-collected", code = parcel.Code });
        if (!collected) return;
        if (parcel.StatusId != 4)
        {
            throw BusinessException.Conflict("Livrez d’abord le colis : le BL se récupère à la livraison.", new { reason = "parcel-not-delivered", code = parcel.Code, statusId = parcel.StatusId });
        }
        if (caller.CourierId is not null && parcel.CourierId != caller.CourierId) throw BusinessException.Forbidden("Ce colis n'est pas affecté à ce livreur.", new { reason = "parcel-other-courier" });
        var now = DateTime.UtcNow;
        var text = string.IsNullOrEmpty(reference) ? null : reference;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var slip = new DeliverySlip
        {
            ParcelId = parcel.Id, ClientId = parcel.ClientId, CourierId = parcel.CourierId, Status = DeliverySlipStatus.COLLECTED, Reference = text, CollectedAt = now, CollectedById = caller.UserId,
            CreatedAt = now,
        };
        db.DeliverySlips.Add(slip);
        db.DeliverySlipEvents.Add(new DeliverySlipEvent { SlipId = slip.Id, Status = DeliverySlipStatus.COLLECTED, Comment = text is null ? "BL récupéré" : $"BL {text} récupéré", AuthorId = caller.UserId, CreatedAt = now });
        db.ParcelEvents.Add(new ParcelEvent
        {
            ParcelId = parcel.Id, StatusId = parcel.StatusId, Comment = text is null ? "BL récupéré par le livreur" : $"BL récupéré par le livreur — réf. {text}", AuthorId = caller.UserId, CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>`collectOnDelivery` : le BL coché au moment de la livraison ; un refus n'annule pas la livraison.</summary>
    public static async Task CollectOnDeliveryAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, string parcelCode, string? reference, CancellationToken cancellationToken)
    {
        try
        {
            await CollectAsync(db, caller, parcelCode, true, reference, cancellationToken);
        }
        catch (BusinessException error)
        {
            db.ChangeTracker.Clear();
            logger.LogWarning("BL non enregistré à la livraison de {Code} : {Message}", parcelCode, error.Message);
        }
    }
}
