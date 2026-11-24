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

/// <summary>`returnHandoverSchema` : le livreur ouvre son bon, ou l'agence le lui demande.</summary>
public sealed record CreateReturnHandoverCommand : ParcelSelection, IRequest<ReturnHandoverDetailDto>
{
    public string? CourierId { get; init; }
    public string? AgencyId { get; init; }
    public string? Note { get; init => field = Zod.Trim(value); }
    public bool? CourierCanEdit { get; init; }
}

public sealed class CreateReturnHandoverValidator : ParcelSelectionValidator<CreateReturnHandoverCommand>
{
    public CreateReturnHandoverValidator()
    {
        RuleFor(x => x.CourierId).MinLength(1);
        RuleFor(x => x.AgencyId).MinLength(1);
        RuleFor(x => x.Note).MaxLength(500);
    }

    protected override void Refine(CreateReturnHandoverCommand value, Action<string, string> issue)
    {
    }
}

internal sealed class CreateReturnHandoverHandler(IAppDbContext db, ICurrentUser caller, ILogger<CreateReturnHandoverHandler> logger) : IRequestHandler<CreateReturnHandoverCommand, ReturnHandoverDetailDto>
{
    public async Task<ReturnHandoverDetailDto> Handle(CreateReturnHandoverCommand input, CancellationToken cancellationToken)
    {
        var listed = (input.ParcelIds ?? []).Distinct().ToList();
        if (caller.CourierId is not null && input.All != true && listed.Count == 0)
        {
            throw BusinessException.Validation("Un bon de retour sans colis ne s’ouvre pas.", new { field = "parcelIds", reason = "handover-empty" });
        }
        var courierId = caller.CourierId ?? input.CourierId;
        if (string.IsNullOrEmpty(courierId)) throw BusinessException.Validation("Le livreur est obligatoire.", new { field = "courierId" });
        var courier = await db.Couriers.AsNoTracking().Where(c => c.Id == courierId && c.DeletedAt == null).Select(c => new { c.Id, c.AgencyId, c.CityId }).FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound("Livreur introuvable.");
        var agencyId = caller.CourierId is not null ? await db.AgencyForCourierAsync(courier.AgencyId, courier.CityId, cancellationToken) ?? input.AgencyId : input.AgencyId;
        if (string.IsNullOrEmpty(agencyId))
        {
            throw BusinessException.Validation(caller.CourierId is not null ? "Aucune agence n’est définie pour la société." : "L’agence est obligatoire.", new { field = "agencyId", reason = "no-agency" });
        }
        await ReturnRows.AssertAgencyReachableAsync(db, caller, agencyId, cancellationToken);
        var origin = caller.CourierId is not null ? ReturnHandoverOrigin.COURIER : ReturnHandoverOrigin.ADMIN;
        var parcelIds = input.All == true
            ? await NoteRules.ResolveSelectionAsync(null, true, input.Exclude,
                async _ => (await Handovers.CandidatesAsync(db, caller, courierId, input.Search, cancellationToken)).Select(l => l.Id).ToList())
            : listed;
        var now = DateTime.UtcNow;
        ReturnHandover created;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            created = await Handovers.OpenAsync(db, caller, courierId, agencyId, origin, parcelIds.Count > 0 ? ReturnHandoverStatus.RETURN_REQUESTED : ReturnHandoverStatus.DRAFT,
                input.CourierCanEdit == true, input.Note, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        db.ChangeTracker.Clear();
        ReturnHandoverDetailDto detail;
        if (parcelIds.Count > 0)
        {
            try
            {
                detail = await Handovers.AddParcelsAsync(db, caller, created.Code, parcelIds, null, null, null, null, cancellationToken);
            }
            catch (BusinessException)
            {
                if (origin == ReturnHandoverOrigin.COURIER) await db.ReturnHandovers.Where(h => h.Id == created.Id).ExecuteDeleteAsync(cancellationToken);
                throw;
            }
        }
        else detail = await Handovers.DetailAsync(db, caller, created.Code, cancellationToken);
        if (origin == ReturnHandoverOrigin.ADMIN && detail.ParcelCount > 0) await Handovers.NotifyCourierAsync(db, logger, courierId, "return-note-requested", detail, cancellationToken);
        return detail;
    }
}
