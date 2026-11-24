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

/// <summary>`BlService` : lectures et étapes partagées.</summary>
public static class BlRows
{
    public const string NotePrefix = "BRB";
    public static readonly string[] SlipStatuses = ["collected", "handed-over", "received", "returned", "closed"];
    public static readonly string[] NoteStatuses = ["draft", "awaiting-receipt", "received", "returned", "closed"];

    private static readonly Dictionary<DeliverySlipStatus, DeliverySlipStatus[]> StepFrom = new()
    {
        [DeliverySlipStatus.HANDED_OVER] = [DeliverySlipStatus.COLLECTED],
        [DeliverySlipStatus.RECEIVED] = [DeliverySlipStatus.COLLECTED, DeliverySlipStatus.HANDED_OVER],
        [DeliverySlipStatus.RETURNED] = [DeliverySlipStatus.RECEIVED],
        [DeliverySlipStatus.CLOSED] = [DeliverySlipStatus.RETURNED],
    };

    public sealed record Moved(string Id, string ClientId, string ParcelCode);

    /// <summary>`slipWhere` : administrateur tout, vendeur les siens, livreur les siens, agent son agence.</summary>
    public static IQueryable<DeliverySlip> Slips(IAppDbContext db, ICurrentUser caller)
    {
        var slips = db.DeliverySlips.AsNoTracking();
        if (caller.IsSuperAdmin()) return slips;
        if (caller.ClientId is { } clientId) return slips.Where(s => s.ClientId == clientId);
        if (caller.CourierId is { } courierId) return slips.Where(s => s.CourierId == courierId);
        if (caller.AgencyId is { } agencyId) return slips.Where(s => s.Parcel.AgencyId == agencyId);
        return slips.Where(_ => false);
    }

    /// <summary>`noteWhere` : administrateur tout, livreur, vendeur, puis agence.</summary>
    public static IQueryable<BlNote> Notes(IQueryable<BlNote> notes, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return notes;
        if (caller.CourierId is { } courierId) return notes.Where(n => n.CourierId == courierId);
        if (caller.ClientId is { } clientId) return notes.Where(n => n.ClientId == clientId);
        if (caller.AgencyId is { } agencyId) return notes.Where(n => n.AgencyId == agencyId);
        return notes.Where(_ => false);
    }

    /// <summary>`parcelWhere` : vendeur, livreur, puis agence.</summary>
    public static IQueryable<Parcel> Parcels(IAppDbContext db, ICurrentUser caller)
    {
        var parcels = db.Parcels.AsNoTracking();
        if (caller.IsSuperAdmin()) return parcels;
        if (caller.ClientId is { } clientId) return parcels.Where(p => p.ClientId == clientId);
        if (caller.CourierId is { } courierId) return parcels.Where(p => p.CourierId == courierId);
        if (caller.AgencyId is { } agencyId) return parcels.Where(p => p.AgencyId == agencyId);
        return parcels.Where(_ => false);
    }

    public static IQueryable<DeliverySlipListItemDto> Project(IQueryable<DeliverySlip> slips) => slips.Select(s => new DeliverySlipListItemDto
    {
        Id = s.Id,
        Status = s.Status == DeliverySlipStatus.COLLECTED ? "collected" : s.Status == DeliverySlipStatus.HANDED_OVER ? "handed-over" : s.Status == DeliverySlipStatus.RECEIVED ? "received"
            : s.Status == DeliverySlipStatus.RETURNED ? "returned" : "closed",
        ParcelId = s.ParcelId,
        ParcelCode = s.Parcel.Code,
        RecipientName = s.Parcel.RecipientName,
        ClientId = s.ClientId,
        ClientName = s.Client.Name,
        CourierId = s.CourierId,
        CourierName = s.Courier != null ? s.Courier.FirstName + " " + s.Courier.LastName : null,
        Reference = s.Reference,
        DeliveredAt = s.Parcel.Events.Where(e => e.StatusId == 4).OrderByDescending(e => e.CreatedAt).Select(e => (DateTime?)e.CreatedAt).FirstOrDefault(),
        CollectedAt = s.CollectedAt,
        HandedOverAt = s.HandedOverAt,
        ReceivedAt = s.ReceivedAt,
        ReturnedAt = s.ReturnedAt,
        ReturnedToName = s.ReturnedToName,
        ClosedAt = s.ClosedAt,
        CreatedAt = s.CreatedAt,
    });

    public static async Task<IQueryable<DeliverySlip>> FilterAsync(IAppDbContext db, ICurrentUser caller, DeliverySlipFilter filter, CancellationToken cancellationToken)
    {
        var where = Slips(db, caller);
        if (!string.IsNullOrEmpty(filter.Status)) { var status = Enum.Parse<DeliverySlipStatus>(Money.FromWire(filter.Status)); where = where.Where(s => s.Status == status); }
        if (filter.ClientId is { } clients && caller.MayFilterBy(caller.ClientId)) where = where.Where(s => clients.Contains(s.ClientId));
        if (filter.CourierId is { } couriers && caller.MayFilterBy(caller.CourierId)) where = where.Where(s => s.CourierId != null && couriers.Contains(s.CourierId));
        var zone = ClockDays.Zone;
        await Task.CompletedTask;
        if (!string.IsNullOrEmpty(filter.From)) { var start = Periods.ZonedDayStart(filter.From, zone); where = where.Where(s => s.CollectedAt >= start); }
        if (!string.IsNullOrEmpty(filter.To)) { var end = Periods.ZonedDayStart(Periods.AddDays(filter.To, 1), zone); where = where.Where(s => s.CollectedAt < end); }
        if (!string.IsNullOrEmpty(filter.Search))
        {
            var needle = filter.Search.ToLower();
            where = where.Where(s => s.Parcel.Code.ToLower().Contains(needle) || s.Parcel.RecipientName.ToLower().Contains(needle)
                                     || (s.Parcel.ExternalCode != null && s.Parcel.ExternalCode.ToLower().Contains(needle)) || (s.Reference != null && s.Reference.ToLower().Contains(needle))
                                     || s.Client.Name.ToLower().Contains(needle));
        }
        return where;
    }

    public static async Task<string> ShortNameAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var company = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        return company is null ? string.Empty : Settings.Dtos.CompanySettingsDto.ShortNameOf(company);
    }

    /// <summary>`advanceSlipsIn` : chaque BL avance s'il est encore à l'étape d'avant ; journal du BL et du colis.</summary>
    public static async Task<List<Moved>> AdvanceSlipsAsync(IAppDbContext db, ICurrentUser caller, IQueryable<DeliverySlip> where, DeliverySlipStatus to, string slipComment, string parcelComment,
        DateTime now, string? returnedToName, CancellationToken cancellationToken)
    {
        var from = StepFrom[to];
        var candidates = await where.Where(s => from.Contains(s.Status)).Select(s => new { s.Id, s.ClientId, s.ParcelId, s.Parcel.Code, s.Parcel.StatusId }).ToListAsync(cancellationToken);
        var moved = new List<Moved>();
        foreach (var slip in candidates)
        {
            var userId = caller.UserId;
            var count = await db.DeliverySlips.Where(s => s.Id == slip.Id && from.Contains(s.Status)).ExecuteUpdateAsync(set => set
                .SetProperty(s => s.Status, to)
                .SetProperty(s => s.HandedOverAt, s => to == DeliverySlipStatus.HANDED_OVER ? now : s.HandedOverAt)
                .SetProperty(s => s.HandedOverById, s => to == DeliverySlipStatus.HANDED_OVER ? userId : s.HandedOverById)
                .SetProperty(s => s.ReceivedAt, s => to == DeliverySlipStatus.RECEIVED ? now : s.ReceivedAt)
                .SetProperty(s => s.ReceivedById, s => to == DeliverySlipStatus.RECEIVED ? userId : s.ReceivedById)
                .SetProperty(s => s.ReturnedAt, s => to == DeliverySlipStatus.RETURNED ? now : s.ReturnedAt)
                .SetProperty(s => s.ReturnedById, s => to == DeliverySlipStatus.RETURNED ? userId : s.ReturnedById)
                .SetProperty(s => s.ReturnedToName, s => to == DeliverySlipStatus.RETURNED && returnedToName != null ? returnedToName : s.ReturnedToName)
                .SetProperty(s => s.ClosedAt, s => to == DeliverySlipStatus.CLOSED ? now : s.ClosedAt)
                .SetProperty(s => s.ClosedById, s => to == DeliverySlipStatus.CLOSED ? userId : s.ClosedById), cancellationToken);
            if (count != 1) continue;
            moved.Add(new Moved(slip.Id, slip.ClientId, slip.Code));
            db.DeliverySlipEvents.Add(new DeliverySlipEvent { SlipId = slip.Id, Status = to, Comment = slipComment, AuthorId = caller.UserId, CreatedAt = now });
            db.ParcelEvents.Add(new ParcelEvent { ParcelId = slip.ParcelId, StatusId = slip.StatusId, Comment = parcelComment, AuthorId = caller.UserId, CreatedAt = now });
        }
        await db.SaveChangesAsync(cancellationToken);
        return moved;
    }

    /// <summary>`notifySlipStep` : « BL prêt » ou « BL remis », un message par vendeur.</summary>
    public static async Task NotifySlipStepAsync(IAppDbContext db, ILogger logger, IReadOnlyList<Moved> moved, DeliverySlipStatus to, string? noteCode, string? shortName,
        CancellationToken cancellationToken)
    {
        if (to is not (DeliverySlipStatus.RECEIVED or DeliverySlipStatus.RETURNED) || moved.Count == 0) return;
        try
        {
            var name = shortName ?? await ShortNameAsync(db, cancellationToken);
            var topic = to == DeliverySlipStatus.RECEIVED ? "bl-ready" : "bl-returned";
            foreach (var group in moved.GroupBy(m => m.ClientId))
            {
                var codes = group.Select(m => m.ParcelCode).ToList();
                var code = noteCode ?? (codes.Count > 3 ? $"{string.Join(", ", codes.Take(3))}…" : string.Join(", ", codes));
                var data = new Dictionary<string, string> { ["topic"] = topic };
                if (noteCode is not null) data["blNoteCode"] = noteCode;
                await Notifications.Notifier.NotifyMerchantAsync(db, group.Key, topic, new Dictionary<string, object> { ["code"] = code, ["count"] = codes.Count, ["shortName"] = name }, data,
                    cancellationToken);
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Notification d'étape BL non envoyée : {Message}", error.Message);
        }
    }

    public static async Task<ParcelBlTrackingDto> TrackingAsync(IAppDbContext db, ICurrentUser caller, string parcelCode, CancellationToken cancellationToken)
    {
        var parcel = await Parcels(db, caller).Where(p => p.Code == parcelCode && p.DeletedAt == null).Select(p => new { p.Id, p.BlRequested }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound($"Colis introuvable : {parcelCode}");
        var slip = await Project(db.DeliverySlips.AsNoTracking().Where(s => s.ParcelId == parcel.Id)).FirstOrDefaultAsync(cancellationToken);
        if (slip is null) return new ParcelBlTrackingDto(parcel.BlRequested, null);
        var steps = await db.DeliverySlipEvents.AsNoTracking().Where(e => e.SlipId == slip.Id).OrderBy(e => e.CreatedAt)
            .Select(e => new { e.Id, e.Status, e.Comment, Author = e.Author != null ? e.Author.FirstName + " " + e.Author.LastName : null, e.CreatedAt }).ToListAsync(cancellationToken);
        var body = System.Text.Json.JsonSerializer.SerializeToNode(slip, System.Text.Json.JsonSerializerOptions.Web)!.AsObject()
            .ToDictionary(kv => kv.Key, kv => (object?)kv.Value?.DeepClone());
        body["steps"] = steps.Select(e => new DeliverySlipStepDto(e.Id, Money.ToWire(e.Status), e.Comment, e.Author, e.CreatedAt)).ToList();
        return new ParcelBlTrackingDto(parcel.BlRequested, body);
    }

    public static IQueryable<BlNoteListItemDto> ProjectNotes(IQueryable<BlNote> notes) => notes.Select(n => new BlNoteListItemDto
    {
        Id = n.Id, Code = n.Code, Status = n.Status == BlNoteStatus.DRAFT ? "draft" : n.Status == BlNoteStatus.AWAITING_RECEIPT ? "awaiting-receipt" : n.Status == BlNoteStatus.RECEIVED ? "received"
            : n.Status == BlNoteStatus.RETURNED ? "returned" : "closed",
        CourierId = n.CourierId, CourierName = n.Courier.FirstName + " " + n.Courier.LastName, ClientId = n.ClientId, ClientName = n.Client.Name, AgencyId = n.AgencyId, AgencyName = n.Agency.Name,
        SlipCount = n.Items.Count, CreatedByName = n.CreatedBy != null ? n.CreatedBy.FirstName + " " + n.CreatedBy.LastName : null, HandedOverAt = n.HandedOverAt, ReceivedAt = n.ReceivedAt,
        ReturnedAt = n.ReturnedAt, ReturnedToName = n.ReturnedToName, ClosedAt = n.ClosedAt, CreatedAt = n.CreatedAt,
    });

    public static async Task<BlNoteDetailDto> NoteDetailAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var scoped = Notes(db.BlNotes.AsNoTracking(), caller).Where(n => n.Code == code);
        var row = await ProjectNotes(scoped).FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Bon BL introuvable : {code}");
        var extra = await scoped.Select(n => new { n.Id, n.Note }).FirstAsync(cancellationToken);
        var itemIds = await db.BlNoteItems.AsNoTracking().Where(i => i.NoteId == extra.Id).OrderBy(i => i.CreatedAt).Select(i => i.SlipId).ToListAsync(cancellationToken);
        var slips = (await Project(db.DeliverySlips.AsNoTracking().Where(s => itemIds.Contains(s.Id))).ToListAsync(cancellationToken)).ToDictionary(s => s.Id);
        var steps = await db.BlNoteEvents.AsNoTracking().Where(e => e.NoteId == extra.Id).OrderBy(e => e.CreatedAt)
            .Select(e => new { e.Id, e.Status, e.Comment, Author = e.Author != null ? e.Author.FirstName + " " + e.Author.LastName : null, e.CreatedAt }).ToListAsync(cancellationToken);
        var detail = new BlNoteDetailDto
        {
            Id = row.Id, Code = row.Code, Status = row.Status, CourierId = row.CourierId, CourierName = row.CourierName, ClientId = row.ClientId, ClientName = row.ClientName, AgencyId = row.AgencyId,
            AgencyName = row.AgencyName, SlipCount = row.SlipCount, CreatedByName = row.CreatedByName, HandedOverAt = row.HandedOverAt, ReceivedAt = row.ReceivedAt, ReturnedAt = row.ReturnedAt,
            ReturnedToName = row.ReturnedToName, ClosedAt = row.ClosedAt, CreatedAt = row.CreatedAt, Note = extra.Note,
        };
        detail.Editable = row.Status == "draft";
        detail.Handoverable = row.Status == "draft" && itemIds.Count > 0;
        detail.Receivable = row.Status == "awaiting-receipt";
        detail.Returnable = row.Status == "received";
        detail.Closable = row.Status == "returned";
        detail.Items = itemIds.Where(slips.ContainsKey).Select(id => slips[id]).ToList();
        detail.Steps = steps.Select(e => new BlNoteStepDto(e.Id, Money.ToWire(e.Status), e.Comment, e.Author, e.CreatedAt)).ToList();
        return detail;
    }
}
