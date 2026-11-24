using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels.Dtos;
using Delivery.Application.Parcels.Queries;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Parcels.Commands;

/// <summary>`DELETE /parcels/:code` : le vendeur supprime son colis avant ramassage.</summary>
public sealed record RemoveParcelBySellerCommand(string Code) : IRequest<ParcelCodeDto>;

public sealed record ParcelCodeDto(string Code);

internal sealed class RemoveParcelBySellerHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveParcelBySellerCommand, ParcelCodeDto>
{
    public async Task<ParcelCodeDto> Handle(RemoveParcelBySellerCommand input, CancellationToken cancellationToken)
    {
        var parcel = await SellerParcels.FindAsync(db, caller, input.Code, cancellationToken);
        var inRoute = await db.RouteParcels.AsNoTracking().Where(l => l.ParcelId == parcel.Id && l.Route.Status != RouteStatus.CLOSED)
            .Select(l => l.Route.Code).FirstOrDefaultAsync(cancellationToken);
        if (inRoute is not null)
        {
            throw BusinessException.Validation($"Le colis {parcel.Code} est sur le circuit {inRoute} : il ne se supprime plus.",
                new { reason = "parcel-in-route", code = parcel.Code, routeCode = inRoute });
        }
        var now = DateTime.UtcNow;
        var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var links = await db.DeliveryNoteParcels.Where(l => l.ParcelId == parcel.Id && l.DeliveryNote.Status != DeliveryNoteStatus.VALIDATED)
            .Include(l => l.DeliveryNote).ToListAsync(cancellationToken);
        db.DeliveryNoteParcels.RemoveRange(links);
        parcel.DeletedAt = now;
        ParcelJournal.Record(db, caller, parcel.Id, "deletedAt", null, Iso(now), ParcelJournal.SellerDeletionReason, author, now);
        ParcelJournal.NoteDepartures(db, caller, parcel.Id, links.Select(l => l.DeliveryNote.Code), ParcelJournal.DeletedPrefix + ParcelJournal.SellerDeletionReason, author, now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ParcelCodeDto(parcel.Code);
    }

    private static string Iso(DateTime value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
