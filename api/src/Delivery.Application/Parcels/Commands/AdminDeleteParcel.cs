using System.Text.Json.Serialization;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.Notifications;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Parcels.Commands;

/// <summary>`POST /parcels/:code/admin-delete` : l'administrateur supprime un colis pas encore pris en charge.</summary>
public sealed record AdminDeleteParcelCommand : IRequest<ParcelCodeDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? Reason { get; init => field = Zod.Trim(value); }
}

public sealed class AdminDeleteParcelValidator : ZodValidator<AdminDeleteParcelCommand>
{
    public AdminDeleteParcelValidator()
    {
        RuleFor(x => x.Reason).Required().MinLength(1, "required").MaxLength(500, "too-long");
    }
}

internal sealed class AdminDeleteParcelHandler(IAppDbContext db, ICurrentUser caller, ILogger<AdminDeleteParcelHandler> logger) : IRequestHandler<AdminDeleteParcelCommand, ParcelCodeDto>
{
    public async Task<ParcelCodeDto> Handle(AdminDeleteParcelCommand input, CancellationToken cancellationToken)
    {
        ParcelAdmin.AssertAdmin(caller);
        var parcel = await ParcelAdmin.ParcelAsync(db, input.Code, cancellationToken);
        if (parcel.Kind == ParcelKind.RETURN)
        {
            throw BusinessException.Validation($"Le colis {parcel.Code} est le retour d’un échange : il se supprime avec son colis d’échange ({ParcelAdmin.ExchangePrefix}-…).",
                new { reason = "parcel-is-return", code = parcel.Code });
        }
        if (!StatusCatalog.IsEditableBySeller(parcel.StatusId))
        {
            throw BusinessException.Validation($"Le colis {parcel.Code} est en « {DocumentLabels.StatusScreen(parcel.StatusId)} » : seul un colis en attente (de ramassage, d’arrivée, ou planifié) se supprime.",
                new { reason = "parcel-not-initial", code = parcel.Code, statusId = parcel.StatusId });
        }
        await ParcelAdmin.AssertNotInOpenRouteAsync(db, parcel.Id, parcel.Code, cancellationToken);
        var exchange = parcel.Kind == ParcelKind.EXCHANGE ? await ParcelAdmin.ExchangeOfAsync(db, parcel, cancellationToken) : null;
        var now = DateTime.UtcNow;
        var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var links = await db.DeliveryNoteParcels.Where(l => l.ParcelId == parcel.Id && l.DeliveryNote.Status != DeliveryNoteStatus.VALIDATED)
                .Include(l => l.DeliveryNote).ToListAsync(cancellationToken);
            db.DeliveryNoteParcels.RemoveRange(links);
            ParcelJournal.NoteDepartures(db, caller, parcel.Id, links.Select(l => l.DeliveryNote.Code), ParcelJournal.DeletedPrefix + input.Reason, author, now);
            parcel.DeletedAt = now;
            ParcelJournal.Record(db, caller, parcel.Id, "deletedAt", null, ParcelAdmin.Iso(now), input.Reason, author, now);
            if (exchange is not null) ParcelAdmin.DropExchange(db, caller, exchange, parcel.Code, input.Reason!, author, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        try
        {
            await Notifier.NotifyMerchantAsync(db, parcel.ClientId, "parcel-deleted",
                new Dictionary<string, object> { ["code"] = parcel.Code, ["reason"] = input.Reason! },
                new Dictionary<string, string> { ["topic"] = "parcel-deleted", ["parcelCode"] = parcel.Code }, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Notification « parcel-deleted » du colis {Code} non envoyée : {Message}", parcel.Code, error.Message);
        }
        return new ParcelCodeDto(parcel.Code);
    }
}
