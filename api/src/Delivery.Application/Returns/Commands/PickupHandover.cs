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

/// <summary>`returnHandoverPickupSchema` : le livreur prend en charge le bon demandé (codes scannés contrôlés).</summary>
public sealed record PickupHandoverCommand : IRequest<ReturnHandoverDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public IReadOnlyList<string>? ParcelCodes { get; init; }
}

public sealed class PickupHandoverValidator : ZodValidator<PickupHandoverCommand>
{
    public PickupHandoverValidator()
    {
        RuleFor(x => x.ParcelCodes).Custom((codes, context) =>
        {
            if (codes is null) return;
            if (codes.Count > 300) context.AddFailure(ValidationPaths.Raw + "parcelCodes", "too-many");
            for (var i = 0; i < codes.Count; i++)
            {
                var code = codes[i]?.Trim() ?? string.Empty;
                if (code.Length < 1) context.AddFailure(ValidationPaths.Raw + $"parcelCodes.{i}", Zod.TooSmallString(1));
                else if (code.Length > 64) context.AddFailure(ValidationPaths.Raw + $"parcelCodes.{i}", Zod.TooBigString(64));
            }
        });
    }
}

internal sealed class PickupHandoverHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<PickupHandoverCommand, ReturnHandoverDetailDto>
{
    public async Task<ReturnHandoverDetailDto> Handle(PickupHandoverCommand input, CancellationToken cancellationToken)
    {
        var handover = await ReturnRows.Handovers(db.ReturnHandovers.AsNoTracking(), caller).Where(h => h.Code == input.Code)
                           .Select(h => new { h.Id, h.Status, h.Origin, Codes = h.Items.Select(i => i.Parcel.Code).ToList() }).FirstOrDefaultAsync(cancellationToken)
                       ?? throw BusinessException.NotFound($"Bon de retour introuvable : {input.Code}");
        if (handover.Origin != ReturnHandoverOrigin.ADMIN)
        {
            throw BusinessException.Conflict("Ce bon a été ouvert par le livreur : il n’y a rien à prendre en charge.", new { reason = "handover-own" });
        }
        if (handover.Status != ReturnHandoverStatus.RETURN_REQUESTED)
        {
            var draft = handover.Status == ReturnHandoverStatus.DRAFT;
            throw BusinessException.Conflict(draft ? "Ce bon est encore un brouillon : il n’a pas été demandé." : "Ce bon est déjà pris en charge.",
                new { reason = draft ? "handover-not-requested" : "handover-already-picked-up" });
        }
        if (handover.Codes.Count == 0) throw BusinessException.Validation("Un bon sans colis ne se prend pas en charge.", new { reason = "handover-empty" });
        foreach (var raw in input.ParcelCodes ?? [])
        {
            var scanned = raw.Trim().ToUpperInvariant();
            if (!handover.Codes.Any(c => c.ToUpperInvariant() == scanned))
            {
                throw BusinessException.Conflict($"Le colis {scanned} n'est pas sur ce bon.", new { reason = "parcel-not-in-handover", code = scanned });
            }
        }
        await Handovers.AdvanceAsync(db, caller, handover.Id, ReturnHandoverStatus.PICKED_UP, "Pris en charge par le livreur", DateTime.UtcNow, cancellationToken);
        db.ChangeTracker.Clear();
        return await Handovers.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
