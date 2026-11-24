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

/// <summary>
/// `returnSlipCloseSchema` : le bon remis au client ; chaque colis passe « Retourné au client », en une transaction, et
/// un colis en échec sans frais reçoit ses frais d'échec. Comme chez Speedy, seuls le nom et le type de la pièce jointe sont gardés.
/// </summary>
public sealed record CloseSlipCommand : IRequest<ReturnSlipDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? ReceivedByName { get; init => field = Zod.Trim(value); }
    public ProofPhotoDto? Proof { get; init; }
}

public sealed class CloseSlipValidator : ZodValidator<CloseSlipCommand>
{
    private static readonly string[] Types = ["application/pdf", "image/jpeg", "image/png", "image/webp"];

    public CloseSlipValidator()
    {
        RuleFor(x => x.ReceivedByName).Required().MinLength(2, "too-short").MaxLength(120);
        RuleFor(x => x.Proof!.Name).Required().MinLength(1, "required").MaxLength(120).OverridePropertyName("proof.name").When(x => x.Proof is not null);
        RuleFor(x => x.Proof!.MimeType).Must(v => v is not null && Types.Contains(v)).WithMessage("unsupported-file-type").OverridePropertyName("proof.mimeType").When(x => x.Proof is not null);
        RuleFor(x => x.Proof!.Content).Required().MinLength(1, "required").OverridePropertyName("proof.content").When(x => x.Proof is not null);
    }
}

internal sealed class CloseSlipHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CloseSlipCommand, ReturnSlipDetailDto>
{
    public async Task<ReturnSlipDetailDto> Handle(CloseSlipCommand input, CancellationToken cancellationToken)
    {
        var slip = await ReturnRows.Slips(db.ReturnSlips.AsNoTracking(), caller).Where(s => s.Code == input.Code).Select(s => new
        {
            s.Id, s.Code, s.Status, s.AgencyId,
            Items = s.Items.Select(i => new { i.Id, i.ParcelId, i.Parcel.StatusId, i.Parcel.IsFailed, i.Parcel.FailureFee, i.Parcel.AgencyId, i.Parcel.OriginCityId, i.Parcel.DestinationCityId }).ToList(),
        }).FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Bon de retour introuvable : {input.Code}");
        if (slip.Status == ReturnSlipStatus.DELIVERED) throw BusinessException.Conflict("Ce bon est déjà remis.", new { reason = "slip-already-delivered" });
        if (slip.Items.Count == 0) throw BusinessException.Validation("Un bon sans colis ne se remet pas.");
        await ReturnRows.AssertAgencyReachableAsync(db, caller, slip.AgencyId, cancellationToken);
        var now = DateTime.UtcNow;
        var policies = new Dictionary<string, PricingPolicy>();
        var pendingFee = new Dictionary<string, long?>();
        foreach (var item in slip.Items.Where(i => i.IsFailed && i.FailureFee is null))
        {
            if (!policies.TryGetValue(item.AgencyId, out var policy))
            {
                policy = await db.PricingPolicyAsync(cancellationToken, item.AgencyId);
                policies[item.AgencyId] = policy;
            }
            pendingFee[item.ParcelId] = FailureFees.For(StatusCatalog.ReturnedToClient, item.OriginCityId == item.DestinationCityId, policy);
        }
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            foreach (var item in slip.Items)
            {
                if (!StatusCatalog.IsReturnEligible(item.StatusId))
                {
                    throw BusinessException.Conflict($"Le colis {item.ParcelId} a changé de statut depuis son ajout.", new { reason = "parcel-moved", parcelId = item.ParcelId });
                }
                var fee = pendingFee.GetValueOrDefault(item.ParcelId);
                if (fee is { } cents)
                {
                    var amount = Money.FromCents(cents);
                    await db.Parcels.Where(p => p.Id == item.ParcelId).ExecuteUpdateAsync(set => set.SetProperty(p => p.StatusId, StatusCatalog.ReturnedToClient).SetProperty(p => p.StatusAt, now)
                        .SetProperty(p => p.FailureFee, amount).SetProperty(p => p.UpdatedAt, now), cancellationToken);
                }
                else
                {
                    await db.Parcels.Where(p => p.Id == item.ParcelId).ExecuteUpdateAsync(set => set.SetProperty(p => p.StatusId, StatusCatalog.ReturnedToClient).SetProperty(p => p.StatusAt, now)
                        .SetProperty(p => p.UpdatedAt, now), cancellationToken);
                }
                db.ParcelEvents.Add(new ParcelEvent
                {
                    ParcelId = item.ParcelId, StatusId = StatusCatalog.ReturnedToClient, Comment = $"Bon de retour {slip.Code}, remis à {input.ReceivedByName}", AuthorId = caller.UserId, CreatedAt = now,
                });
                await db.ReturnSlipItems.Where(i => i.Id == item.Id).ExecuteUpdateAsync(set => set.SetProperty(i => i.DeliveredParcelId, item.ParcelId), cancellationToken);
            }
            await db.ReturnSlips.Where(s => s.Id == slip.Id).ExecuteUpdateAsync(set => set.SetProperty(s => s.Status, ReturnSlipStatus.DELIVERED).SetProperty(s => s.DeliveredAt, now)
                .SetProperty(s => s.DeliveredById, caller.UserId).SetProperty(s => s.ReceivedByName, input.ReceivedByName).SetProperty(s => s.ProofName, input.Proof == null ? null : input.Proof.Name)
                .SetProperty(s => s.ProofMimeType, input.Proof == null ? null : input.Proof.MimeType).SetProperty(s => s.UpdatedAt, now), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await Slips.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
