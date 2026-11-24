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

/// <summary>`ReturnsService` côté bons de retour client (BRT-).</summary>
public static class Slips
{
    public static IQueryable<ReturnSlipListItemDto> Project(IQueryable<ReturnSlip> slips) => slips.Select(s => new ReturnSlipListItemDto
    {
        Id = s.Id, Code = s.Code, Status = s.Status == ReturnSlipStatus.DRAFT ? "draft" : "delivered", ClientId = s.ClientId, ClientName = s.Client.Name, AgencyId = s.AgencyId,
        AgencyName = s.Agency.Name, ParcelCount = s.Items.Count, TotalFees = s.Items.Sum(i => (long)Math.Round(i.FeeApplied * 100m)), DeliveredAt = s.DeliveredAt, ReceivedByName = s.ReceivedByName,
        DeliveredByName = s.DeliveredBy != null ? (s.DeliveredBy.FirstName + " " + s.DeliveredBy.LastName).Trim() : null, CreatedAt = s.CreatedAt,
    });

    public static async Task<ReturnSlipDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var scoped = ReturnRows.Slips(db.ReturnSlips.AsNoTracking(), caller).Where(s => s.Code == code);
        var row = await Project(scoped).FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Bon de retour introuvable : {code}");
        var note = await scoped.Select(s => s.Note).FirstAsync(cancellationToken);
        var items = await db.ReturnSlipItems.AsNoTracking().Where(i => i.SlipId == row.Id).OrderBy(i => i.CreatedAt)
            .Select(i => new { i.Id, i.ParcelId, i.Parcel.Code, i.Parcel.RecipientName, City = i.Parcel.DestinationCity.Name, i.Reason, i.FeeApplied, i.Parcel.StatusId, i.Parcel.CreatedAt, i.Parcel.Quantity })
            .ToListAsync(cancellationToken);
        return new ReturnSlipDetailDto
        {
            Id = row.Id, Code = row.Code, Status = row.Status, ClientId = row.ClientId, ClientName = row.ClientName, AgencyId = row.AgencyId, AgencyName = row.AgencyName, ParcelCount = row.ParcelCount,
            TotalFees = row.TotalFees, DeliveredAt = row.DeliveredAt, ReceivedByName = row.ReceivedByName, DeliveredByName = row.DeliveredByName, CreatedAt = row.CreatedAt, Note = note,
            Closable = row.Status == "draft" && items.Count > 0,
            Items = items.Select(i => new ReturnSlipItemDto(i.Id, i.ParcelId, i.Code, i.RecipientName, i.City, Money.ToWire(i.Reason), Money.ToCents(i.FeeApplied), i.StatusId, i.CreatedAt, i.Quantity))
                .ToList(),
        };
    }

    public static async Task<ReturnSlip> OpenAsync(IAppDbContext db, ICurrentUser caller, string clientId, string agencyId, string? note, DateTime now, CancellationToken cancellationToken)
    {
        var code = await DocumentCodes.ReserveAsync(db, ReturnRows.SlipPrefix, now, cancellationToken);
        var slip = new ReturnSlip { Code = code, ClientId = clientId, AgencyId = agencyId, Note = ReturnRows.Text(note), CreatedById = caller.UserId, CreatedAt = now, UpdatedAt = now };
        db.ReturnSlips.Add(slip);
        await db.SaveChangesAsync(cancellationToken);
        return slip;
    }

    /// <summary>`slipCandidates` : les colis rentrés en agence de cet e-commerçant (rien pour un bon remis).</summary>
    public static async Task<List<ReturnParcelLineDto>> CandidatesAsync(IAppDbContext db, ICurrentUser caller, string code, string? search, CancellationToken cancellationToken)
    {
        var slip = await ReturnRows.Slips(db.ReturnSlips.AsNoTracking(), caller).Where(s => s.Code == code).Select(s => new { s.ClientId, s.Status }).FirstOrDefaultAsync(cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon de retour introuvable : {code}");
        if (slip.Status != ReturnSlipStatus.DRAFT) return [];
        var where = ReturnRows.SlipEligible(db.Parcels.AsNoTracking().Where(p => p.ClientId == slip.ClientId && p.DeletedAt == null));
        if (!string.IsNullOrWhiteSpace(search)) where = ParcelFilters.Search(where, search);
        return await ReturnRows.Lines(where.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Code).Take(NoteRules.SelectionMax + 1)).ToListAsync(cancellationToken);
    }

    /// <summary>`insertSlipParcel` : rentré en agence, du même vendeur, sur aucun autre bon ; frais d'échec de la politique de l'agence.</summary>
    public static async Task InsertAsync(IAppDbContext db, ICurrentUser caller, string code, string parcelId, ReturnReason reason, CancellationToken cancellationToken)
    {
        var slip = await ReturnRows.Slips(db.ReturnSlips.AsNoTracking(), caller).Where(s => s.Code == code).Select(s => new { s.Id, s.Status, s.ClientId, s.AgencyId }).FirstOrDefaultAsync(cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon de retour introuvable : {code}");
        if (slip.Status != ReturnSlipStatus.DRAFT) throw BusinessException.Conflict("Ce bon est remis : il ne se modifie plus.", new { reason = "slip-delivered" });
        var parcel = await db.Parcels.AsNoTracking().Where(p => p.Id == parcelId && p.DeletedAt == null)
                         .Select(p => new { p.Id, p.Code, p.StatusId, p.ClientId, p.OriginCityId, p.DestinationCityId }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound("Colis introuvable.");
        var received = await db.ReturnHandoverItems.AsNoTracking()
            .AnyAsync(i => i.ParcelId == parcel.Id && i.CorrectedAt == null && (i.Handover.Status == ReturnHandoverStatus.CONFIRMED || i.Handover.Status == ReturnHandoverStatus.CLOSED), cancellationToken);
        if (!received)
        {
            throw BusinessException.Conflict($"Le colis {parcel.Code} n'est pas rentré en agence : aucun bon de retour livreur reçu ne le porte.",
                new { reason = "parcel-not-received", code = parcel.Code, statusId = parcel.StatusId });
        }
        if (parcel.ClientId != slip.ClientId) throw BusinessException.Conflict($"Le colis {parcel.Code} appartient à un autre e-commerçant.", new { reason = "parcel-other-client", code = parcel.Code });
        var taken = await db.ReturnSlipItems.AsNoTracking().Where(i => i.ParcelId == parcel.Id).Select(i => new { i.Slip.Code, i.Slip.Status }).FirstOrDefaultAsync(cancellationToken);
        if (taken is not null)
        {
            throw BusinessException.Conflict(taken.Status == ReturnSlipStatus.DELIVERED
                    ? $"Le colis {parcel.Code} a déjà été rendu par le bon {taken.Code}."
                    : $"Le colis {parcel.Code} est déjà sur le bon {taken.Code}.",
                new { reason = "parcel-already-returned", code = parcel.Code });
        }
        var policy = await db.PricingPolicyAsync(cancellationToken, slip.AgencyId);
        try
        {
            db.ReturnSlipItems.Add(new ReturnSlipItem
            {
                SlipId = slip.Id, ParcelId = parcel.Id, Reason = reason, FeeApplied = Money.FromCents(ReturnRows.SlipLineFee(policy, parcel.OriginCityId, parcel.DestinationCityId)),
                ReservedParcelId = parcel.Id, CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            throw BusinessException.Conflict($"Le colis {parcel.Code} est déjà sur un bon de retour.", new { reason = "parcel-already-returned", code = parcel.Code });
        }
    }

    public static ReturnReason Reason(string? wire) => Enum.Parse<ReturnReason>(Money.FromWire(wire ?? "other"));

    /// <summary>`bulkSlipGroups` : un groupe par vendeur et par agence de réception (le bon reçu le plus récent).</summary>
    public static async Task<List<(string OwnerId, string AgencyId, List<(string Id, string OriginCityId, string DestinationCityId)> Parcels)>> BulkGroupsAsync(IAppDbContext db, ICurrentUser caller,
        CancellationToken cancellationToken)
    {
        var parcels = await ReturnRows.SlipEligible(db.Parcels.AsNoTracking().Where(p => p.DeletedAt == null)).OrderBy(p => p.StatusAt).ThenBy(p => p.Code)
            .Select(p => new
            {
                p.Id, p.ClientId, p.OriginCityId, p.DestinationCityId,
                Received = p.HandoverItems.Where(i => i.Handover.Status == ReturnHandoverStatus.CONFIRMED || i.Handover.Status == ReturnHandoverStatus.CLOSED)
                    .Select(i => new { i.Handover.AgencyId, At = i.Handover.ConfirmedAt ?? i.Handover.CreatedAt }).ToList(),
            }).ToListAsync(cancellationToken);
        var reachable = await ReturnRows.ReachableAgenciesAsync(db, caller, cancellationToken);
        var groups = new List<(string OwnerId, string AgencyId, List<(string Id, string OriginCityId, string DestinationCityId)> Parcels)>();
        foreach (var parcel in parcels)
        {
            var received = parcel.Received.OrderByDescending(r => r.At).FirstOrDefault();
            if (received is null || (reachable is not null && !reachable.Contains(received.AgencyId))) continue;
            var index = groups.FindIndex(g => g.OwnerId == parcel.ClientId && g.AgencyId == received.AgencyId);
            if (index < 0) groups.Add((parcel.ClientId, received.AgencyId, [(parcel.Id, parcel.OriginCityId, parcel.DestinationCityId)]));
            else groups[index].Parcels.Add((parcel.Id, parcel.OriginCityId, parcel.DestinationCityId));
        }
        return groups;
    }
}
