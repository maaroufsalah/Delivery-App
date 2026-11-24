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

/// <summary>`POST /return-slips/bulk` : un brouillon par vendeur et par agence avec ses colis rentrés ; verrou en base.</summary>
public sealed record CreateSlipsInBulkCommand : IRequest<ReturnBulkSummaryDto>;

internal sealed class CreateSlipsInBulkHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateSlipsInBulkCommand, ReturnBulkSummaryDto>
{
    public async Task<ReturnBulkSummaryDto> Handle(CreateSlipsInBulkCommand request, CancellationToken cancellationToken)
    {
        ReturnRows.AssertBackOffice(caller);
        var now = DateTime.UtcNow;
        var policies = new Dictionary<string, PricingPolicy>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('return-slip-bulk'))", cancellationToken);
        var groups = await Slips.BulkGroupsAsync(db, caller, cancellationToken);
        var codes = new List<string>();
        foreach (var group in groups)
        {
            var slip = await Slips.OpenAsync(db, caller, group.OwnerId, group.AgencyId, null, now, cancellationToken);
            if (!policies.TryGetValue(group.AgencyId, out var policy))
            {
                policy = await db.PricingPolicyAsync(cancellationToken, group.AgencyId);
                policies[group.AgencyId] = policy;
            }
            foreach (var parcel in group.Parcels)
            {
                db.ReturnSlipItems.Add(new ReturnSlipItem
                {
                    SlipId = slip.Id, ParcelId = parcel.Id, Reason = ReturnReason.OTHER, FeeApplied = Money.FromCents(ReturnRows.SlipLineFee(policy, parcel.OriginCityId, parcel.DestinationCityId)),
                    ReservedParcelId = parcel.Id, CreatedAt = now,
                });
            }
            await db.SaveChangesAsync(cancellationToken);
            codes.Add(slip.Code);
        }
        await transaction.CommitAsync(cancellationToken);
        return ReturnRows.Summarize(groups, codes);
    }
}
