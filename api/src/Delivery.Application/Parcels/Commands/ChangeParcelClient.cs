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

/// <summary>`POST /parcels/:code/client` : passe le colis à un autre client (motif, empreinte de l'aperçu).</summary>
public sealed record ChangeParcelClientCommand : IRequest<ParcelClientChangeResultDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? ClientId { get; init => field = Zod.Trim(value); }

    public string? Reason { get; init => field = Zod.Trim(value); }

    public string? Fingerprint { get; init => field = Zod.Trim(value); }
}

public sealed class ChangeParcelClientValidator : ZodValidator<ChangeParcelClientCommand>
{
    public ChangeParcelClientValidator()
    {
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.Reason).Required().MinLength(1, "required").MaxLength(500, "too-long");
    }
}

internal sealed class ChangeParcelClientHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<ChangeParcelClientCommand, ParcelClientChangeResultDto>
{
    public async Task<ParcelClientChangeResultDto> Handle(ChangeParcelClientCommand input, CancellationToken cancellationToken)
    {
        ParcelAdmin.AssertAdmin(caller);
        var plan = await ParcelAdmin.PlanAsync(db, input.Code, input.ClientId!, cancellationToken);
        if (!string.IsNullOrEmpty(input.Fingerprint) && input.Fingerprint != plan.Preview.Fingerprint)
        {
            throw BusinessException.Conflict("Le colis a changé depuis l’aperçu : relisez-le avant de valider.", new { reason = "preview-stale" });
        }
        var (parcel, preview) = (plan.Parcel, plan.Preview);
        var now = DateTime.UtcNow;
        var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var locked = await db.Database.SqlQuery<string>($"""
            SELECT id AS "Value" FROM parcels
             WHERE id = {parcel.Id} AND "clientId" = {parcel.ClientId} AND "deletedAt" IS NULL
               AND "financialStatus" = 'NON_ENCAISSE' AND "billingState" = 'TO_INVOICE'
               AND "reimbursedByCourier" = false
             FOR UPDATE
            """).ToListAsync(cancellationToken);
        if (locked.Count != 1) throw BusinessException.Conflict("Le colis a changé pendant la correction : relisez-le avant de valider.", new { reason = "preview-stale" });
        plan.Apply(parcel);
        if (plan.Detached.Count > 0)
        {
            var ids = plan.Detached.Select(d => d.Id).ToList();
            db.DeliveryNoteParcels.RemoveRange(await db.DeliveryNoteParcels.Where(l => l.ParcelId == parcel.Id && ids.Contains(l.DeliveryNoteId)).ToListAsync(cancellationToken));
            ParcelJournal.NoteDepartures(db, caller, parcel.Id, plan.Detached.Select(d => d.Code), ParcelJournal.ClientChangePrefix + input.Reason, author, now);
        }
        foreach (var change in preview.Changes) ParcelJournal.Record(db, caller, parcel.Id, change.Field, change.Before, change.After, input.Reason, author, now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ParcelClientChangeResultDto(parcel.Code, preview.To.Id, preview.FeeBefore, preview.FeeAfter, preview.Notes);
    }
}
