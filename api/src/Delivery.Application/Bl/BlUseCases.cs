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

public sealed class DeliverySlipListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ParcelId { get; set; } = string.Empty;
    public string ParcelCode { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string? CourierId { get; set; }
    public string? CourierName { get; set; }
    public string? Reference { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CollectedAt { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnedToName { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record DeliverySlipStepDto(string Id, string Status, string? Comment, string? AuthorName, DateTime CreatedAt);

public sealed class DeliverySlipDetailDto
{
    [JsonPropertyOrder(-1)] public DeliverySlipListItemDto Slip { get; set; } = new();
}

public sealed record ParcelBlTrackingDto(bool Requested, Dictionary<string, object?>? Slip);

public class BlNoteListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CourierId { get; set; } = string.Empty;
    public string CourierName { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string AgencyName { get; set; } = string.Empty;
    public int SlipCount { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnedToName { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record BlNoteStepDto(string Id, string Status, string? Comment, string? AuthorName, DateTime CreatedAt);

public sealed class BlNoteDetailDto : BlNoteListItemDto
{
    [JsonPropertyOrder(1)] public string? Note { get; set; }
    [JsonPropertyOrder(1)] public bool Editable { get; set; }
    [JsonPropertyOrder(1)] public bool Handoverable { get; set; }
    [JsonPropertyOrder(1)] public bool Receivable { get; set; }
    [JsonPropertyOrder(1)] public bool Returnable { get; set; }
    [JsonPropertyOrder(1)] public bool Closable { get; set; }
    [JsonPropertyOrder(1)] public List<DeliverySlipListItemDto> Items { get; set; } = [];
    [JsonPropertyOrder(1)] public List<BlNoteStepDto> Steps { get; set; } = [];
}

public sealed record DeliverySlipAdvanceResultDto(int Count, int Skipped);

/// <summary>`deliverySlipFiltersSchema` (+ listes multi-valeurs et pagination).</summary>
public sealed record DeliverySlipFilter(string? Search, string? Status, IReadOnlyList<string>? ClientId, IReadOnlyList<string>? CourierId, string? From, string? To);

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

/// <summary>`blCollectSchema` : le livreur récupère (ou non) le BL signé à la livraison.</summary>
public sealed record CollectBlCommand : IRequest<ParcelBlTrackingDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public bool? Collected { get; init; }
    public string? Reference { get; init => field = Zod.Trim(value); }
}

public sealed class CollectBlValidator : ZodValidator<CollectBlCommand>
{
    public CollectBlValidator()
    {
        RuleFor(x => x.Collected).Required("boolean");
        RuleFor(x => x.Reference).MaxLength(60);
    }
}

internal sealed class CollectBlHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CollectBlCommand, ParcelBlTrackingDto>
{
    public async Task<ParcelBlTrackingDto> Handle(CollectBlCommand input, CancellationToken cancellationToken)
    {
        await BlCollection.CollectAsync(db, caller, input.Code, input.Collected == true, input.Reference, cancellationToken);
        return await BlRows.TrackingAsync(db, caller, input.Code, cancellationToken);
    }
}

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

/// <summary>`GET /bl/parcels/:code`.</summary>
public sealed record GetParcelBlQuery(string Code) : IRequest<ParcelBlTrackingDto>;

internal sealed class GetParcelBlHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetParcelBlQuery, ParcelBlTrackingDto>
{
    public Task<ParcelBlTrackingDto> Handle(GetParcelBlQuery request, CancellationToken cancellationToken) => BlRows.TrackingAsync(db, caller, request.Code, cancellationToken);
}

/// <summary>`GET /bl/delivery-slips`.</summary>
public sealed record GetDeliverySlipsQuery(ListQuery Paging, DeliverySlipFilter Filter) : IRequest<Paginated<DeliverySlipListItemDto>>;

public sealed class GetDeliverySlipsValidator : ZodValidator<GetDeliverySlipsQuery>
{
    public GetDeliverySlipsValidator()
    {
        RuleFor(x => x.Filter.Status).OneOf(BlRows.SlipStatuses).OverridePropertyName("status");
    }
}

internal sealed class GetDeliverySlipsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetDeliverySlipsQuery, Paginated<DeliverySlipListItemDto>>
{
    public async Task<Paginated<DeliverySlipListItemDto>> Handle(GetDeliverySlipsQuery request, CancellationToken cancellationToken)
    {
        var where = await BlRows.FilterAsync(db, caller, request.Filter, cancellationToken);
        var total = await where.CountAsync(cancellationToken);
        var ordered = request.Paging.Order == "asc" ? where.OrderBy(s => s.CollectedAt).ThenBy(s => s.Id) : where.OrderByDescending(s => s.CollectedAt).ThenBy(s => s.Id);
        var items = await BlRows.Project(ordered.Skip((request.Paging.Page - 1) * request.Paging.PageSize).Take(request.Paging.PageSize)).ToListAsync(cancellationToken);
        return new Paginated<DeliverySlipListItemDto>(items, total, request.Paging.Page, request.Paging.PageSize);
    }
}

/// <summary>`GET /bl/delivery-slips/export` : classeur « Bons de livraison ».</summary>
public sealed record ExportDeliverySlipsQuery(ListQuery Paging, DeliverySlipFilter Filter) : IRequest<FileContent>;

public sealed class ExportDeliverySlipsValidator : ZodValidator<ExportDeliverySlipsQuery>
{
    public ExportDeliverySlipsValidator()
    {
        RuleFor(x => x.Filter.Status).OneOf(BlRows.SlipStatuses).OverridePropertyName("status");
    }
}

internal sealed class ExportDeliverySlipsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportDeliverySlipsQuery, FileContent>
{
    private static readonly Dictionary<string, string> States = new()
    {
        ["collected"] = "Récupéré par le livreur", ["handed-over"] = "Remis à l’agence (liasse)", ["received"] = "Réceptionné", ["returned"] = "Récupéré par le vendeur", ["closed"] = "Clôturé",
    };

    private static readonly IReadOnlyList<ExportColumn<DeliverySlipListItemDto>> Columns =
    [
        new("Colis", r => r.ParcelCode),
        new("Destinataire", r => r.RecipientName),
        new("Client Pro", r => r.ClientName),
        new("Livreur", r => r.CourierName ?? string.Empty),
        new("Référence BL", r => r.Reference ?? string.Empty),
        new("Livré le", r => Exports.DateTimeText(r.DeliveredAt)),
        new("Récupéré le", r => Exports.DateTimeText(r.CollectedAt)),
        new("Réceptionné le", r => Exports.DateTimeText(r.ReceivedAt)),
        new("Remis au vendeur le", r => Exports.DateTimeText(r.ReturnedAt)),
        new("Remis à", r => r.ReturnedToName ?? string.Empty),
        new("État", r => States[r.Status]),
    ];

    public async Task<FileContent> Handle(ExportDeliverySlipsQuery request, CancellationToken cancellationToken)
    {
        var where = await BlRows.FilterAsync(db, caller, request.Filter, cancellationToken);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var ordered = request.Paging.Order == "asc" ? where.OrderBy(s => s.CollectedAt).ThenBy(s => s.Id) : where.OrderByDescending(s => s.CollectedAt).ThenBy(s => s.Id);
        var rows = await BlRows.Project(ordered).ToListAsync(cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Bons de livraison", Columns, rows, cancellationToken);
    }
}

/// <summary>`deliverySlipAdvanceSchema` : réceptionner, ou remettre au vendeur, des BL cochés ou « tout ».</summary>
public sealed record AdvanceDeliverySlipsCommand : IRequest<DeliverySlipAdvanceResultDto>
{
    public string? To { get; init; }
    public IReadOnlyList<string>? Ids { get; init; }
    public bool? All { get; init; }
    public string? Search { get; init => field = Zod.Trim(value); }
    public IReadOnlyList<string>? Exclude { get; init; }
    public DeliverySlipFiltersBody? Filters { get; init; }
    public string? ReturnedToName { get; init => field = Zod.Trim(value); }
}

public sealed record DeliverySlipFiltersBody
{
    public string? Search { get; init => field = Zod.Trim(value); }
    public string? Status { get; init; }
    public string? ClientId { get; init => field = Zod.Trim(value); }
    public string? CourierId { get; init => field = Zod.Trim(value); }
    public string? From { get; init => field = Zod.Trim(value); }
    public string? To { get; init => field = Zod.Trim(value); }
}

public sealed class AdvanceDeliverySlipsValidator : ZodValidator<AdvanceDeliverySlipsCommand>
{
    public AdvanceDeliverySlipsValidator()
    {
        RuleFor(x => x.To).Required().OneOf(["received", "returned"]);
        RuleFor(x => x.ReturnedToName).MinLength(2, "too-short").MaxLength(120);
        RuleFor(x => x.Search).MaxLength(120);
    }

    protected override void Refine(AdvanceDeliverySlipsCommand value, Action<string, string> issue)
    {
        if (value.All != true && (value.Ids?.Count ?? 0) == 0) issue(ValidationPaths.Raw + "ids", "required");
        if (value.To == "returned" && string.IsNullOrEmpty(value.ReturnedToName)) issue(ValidationPaths.Raw + "returnedToName", "required");
    }
}

internal sealed class AdvanceDeliverySlipsHandler(IAppDbContext db, ICurrentUser caller, ILogger<AdvanceDeliverySlipsHandler> logger) : IRequestHandler<AdvanceDeliverySlipsCommand, DeliverySlipAdvanceResultDto>
{
    public async Task<DeliverySlipAdvanceResultDto> Handle(AdvanceDeliverySlipsCommand input, CancellationToken cancellationToken)
    {
        var f = input.Filters ?? new DeliverySlipFiltersBody();
        var filter = new DeliverySlipFilter(f.Search, f.Status, f.ClientId is null ? null : [f.ClientId], f.CourierId is null ? null : [f.CourierId], f.From, f.To);
        var ids = await NoteRules.ResolveSelectionAsync(input.Ids, input.All, input.Exclude, async take =>
            await (await BlRows.FilterAsync(db, caller, filter, cancellationToken)).OrderByDescending(s => s.CollectedAt).ThenBy(s => s.Id).Select(s => s.Id).Take(take).ToListAsync(cancellationToken));
        var to = input.To == "received" ? DeliverySlipStatus.RECEIVED : DeliverySlipStatus.RETURNED;
        var shortName = await BlRows.ShortNameAsync(db, cancellationToken);
        var comment = to == DeliverySlipStatus.RECEIVED ? $"Réceptionné par {shortName}" : $"Récupéré par le vendeur — remis à {input.ReturnedToName ?? string.Empty}";
        List<BlRows.Moved> moved;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            moved = await BlRows.AdvanceSlipsAsync(db, caller, BlRows.Slips(db, caller).Where(s => ids.Contains(s.Id)), to, comment, $"BL — {comment}", DateTime.UtcNow, input.ReturnedToName,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        await BlRows.NotifySlipStepAsync(db, logger, moved, to, null, shortName, cancellationToken);
        return new DeliverySlipAdvanceResultDto(moved.Count, ids.Count - moved.Count);
    }
}

/// <summary>`deliverySlipRevertSchema` : l'administrateur fait reculer un BL d'une étape.</summary>
public sealed record RevertDeliverySlipCommand : IRequest<DeliverySlipListItemDto>
{
    [JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public string? Reason { get; init => field = Zod.Trim(value); }
}

public sealed class RevertDeliverySlipValidator : ZodValidator<RevertDeliverySlipCommand>
{
    public RevertDeliverySlipValidator()
    {
        RuleFor(x => x.Reason).Required().MinLength(1, "required").MaxLength(255);
    }
}

internal sealed class RevertDeliverySlipHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RevertDeliverySlipCommand, DeliverySlipListItemDto>
{
    public async Task<DeliverySlipListItemDto> Handle(RevertDeliverySlipCommand input, CancellationToken cancellationToken)
    {
        if (!caller.IsSuperAdmin()) throw BusinessException.Forbidden("Seul un administrateur revient sur l’état d’un BL.");
        var slip = await BlRows.Slips(db, caller).Where(s => s.Id == input.Id).Select(s => new
        {
            s.Id, s.Status, s.ParcelId, s.Parcel.StatusId, Carrier = s.NoteItems.Where(i => i.Note.Status != BlNoteStatus.DRAFT).Select(i => i.Note.Code).FirstOrDefault(),
        }).FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound("BL introuvable.");
        if (slip.Carrier is not null) throw BusinessException.Conflict($"Ce BL est porté par le bon {slip.Carrier} : il suit son bon.", new { reason = "bl-on-note", code = slip.Carrier });
        DeliverySlipStatus back = slip.Status switch
        {
            DeliverySlipStatus.RETURNED => DeliverySlipStatus.RECEIVED,
            DeliverySlipStatus.RECEIVED => DeliverySlipStatus.COLLECTED,
            _ => throw BusinessException.Conflict("Ce BL est à sa première étape : il n’y a rien à défaire.", new { reason = "bl-revert-impossible" }),
        };
        var now = DateTime.UtcNow;
        var comment = $"Retour arrière : {input.Reason}";
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var from = slip.Status;
            var count = back == DeliverySlipStatus.RECEIVED
                ? await db.DeliverySlips.Where(s => s.Id == slip.Id && s.Status == from).ExecuteUpdateAsync(set => set.SetProperty(s => s.Status, back)
                    .SetProperty(s => s.ReturnedAt, (DateTime?)null).SetProperty(s => s.ReturnedById, (string?)null), cancellationToken)
                : await db.DeliverySlips.Where(s => s.Id == slip.Id && s.Status == from).ExecuteUpdateAsync(set => set.SetProperty(s => s.Status, back)
                    .SetProperty(s => s.ReceivedAt, (DateTime?)null).SetProperty(s => s.ReceivedById, (string?)null).SetProperty(s => s.HandedOverAt, (DateTime?)null)
                    .SetProperty(s => s.HandedOverById, (string?)null), cancellationToken);
            if (count == 0) throw BusinessException.Conflict("Ce BL a changé d’état entre-temps : rechargez.", new { reason = "bl-moved" });
            db.DeliverySlipEvents.Add(new DeliverySlipEvent { SlipId = slip.Id, Status = back, Comment = comment, AuthorId = caller.UserId, CreatedAt = now });
            db.ParcelEvents.Add(new ParcelEvent { ParcelId = slip.ParcelId, StatusId = slip.StatusId, Comment = $"BL — {comment}", AuthorId = caller.UserId, CreatedAt = now });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await BlRows.Project(db.DeliverySlips.AsNoTracking().Where(s => s.Id == slip.Id)).FirstAsync(cancellationToken);
    }
}

/// <summary>`GET /bl/slips` (`blSlipListQuerySchema`) : les BL encore en sacoche, libres de tout bon.</summary>
public sealed record GetCollectableSlipsQuery(string? ClientId, string? CourierId) : IRequest<IReadOnlyList<DeliverySlipListItemDto>>;

internal sealed class GetCollectableSlipsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCollectableSlipsQuery, IReadOnlyList<DeliverySlipListItemDto>>
{
    public async Task<IReadOnlyList<DeliverySlipListItemDto>> Handle(GetCollectableSlipsQuery request, CancellationToken cancellationToken)
    {
        var slips = db.DeliverySlips.AsNoTracking().Where(s => s.Status == DeliverySlipStatus.COLLECTED && !db.BlNoteItems.Any(i => i.ReservedSlipId == s.Id));
        if (caller.ClientId is { } clientId) slips = slips.Where(s => s.ClientId == clientId);
        if (caller.CourierId is { } courierId) slips = slips.Where(s => s.CourierId == courierId);
        if (!string.IsNullOrEmpty(request.ClientId) && caller.MayFilterBy(caller.ClientId)) slips = slips.Where(s => s.ClientId == request.ClientId);
        if (!string.IsNullOrEmpty(request.CourierId) && caller.MayFilterBy(caller.CourierId)) slips = slips.Where(s => s.CourierId == request.CourierId);
        return await BlRows.Project(slips.OrderBy(s => s.CollectedAt).Take(NoteRules.SelectionMax + 1)).ToListAsync(cancellationToken);
    }
}

/// <summary>`blNoteListQuerySchema`.</summary>
public sealed record BlNoteFilter(ListQuery Paging, IReadOnlyList<string>? Status, IReadOnlyList<string>? ClientId, IReadOnlyList<string>? CourierId, IReadOnlyList<string>? AgencyId);

internal static class BlNoteLists
{
    public static IQueryable<BlNote> Filter(IAppDbContext db, ICurrentUser caller, BlNoteFilter filter)
    {
        var where = BlRows.Notes(db.BlNotes.AsNoTracking(), caller);
        if (filter.Status is { } statuses)
        {
            var values = statuses.Select(s => Enum.Parse<BlNoteStatus>(Money.FromWire(s))).ToList();
            where = where.Where(n => values.Contains(n.Status));
        }
        if (filter.ClientId is { } clients && caller.MayFilterBy(caller.ClientId)) where = where.Where(n => clients.Contains(n.ClientId));
        if (filter.CourierId is { } couriers && caller.MayFilterBy(caller.CourierId)) where = where.Where(n => couriers.Contains(n.CourierId));
        if (filter.AgencyId is { } agencies && caller.MayFilterBy(caller.AgencyId)) where = where.Where(n => agencies.Contains(n.AgencyId));
        if (filter.Paging.Search is { Length: > 0 } search)
        {
            var needle = search.ToLower();
            where = where.Where(n => n.Code.ToLower().Contains(needle) || n.Client.Name.ToLower().Contains(needle));
        }
        return filter.Paging.Order == "asc" ? where.OrderBy(n => n.CreatedAt) : where.OrderByDescending(n => n.CreatedAt);
    }

    public static void Validate(BlNoteFilter filter)
    {
        var bad = filter.Status?.Where(s => !BlRows.NoteStatuses.Contains(s)).ToList();
        if (bad is { Count: > 0 })
        {
            throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "status", message = Zod.InvalidOption(BlRows.NoteStatuses) } } });
        }
    }
}

/// <summary>`GET /bl/notes`.</summary>
public sealed record GetBlNotesQuery(BlNoteFilter Filter) : IRequest<Paginated<BlNoteListItemDto>>;

internal sealed class GetBlNotesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetBlNotesQuery, Paginated<BlNoteListItemDto>>
{
    public async Task<Paginated<BlNoteListItemDto>> Handle(GetBlNotesQuery request, CancellationToken cancellationToken)
    {
        BlNoteLists.Validate(request.Filter);
        var paging = request.Filter.Paging;
        var where = BlNoteLists.Filter(db, caller, request.Filter);
        var total = await where.CountAsync(cancellationToken);
        var items = await BlRows.ProjectNotes(where.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)).ToListAsync(cancellationToken);
        return new Paginated<BlNoteListItemDto>(items, total, paging.Page, paging.PageSize);
    }
}

/// <summary>`GET /bl/notes/export` : classeur « Bons BL ».</summary>
public sealed record ExportBlNotesQuery(BlNoteFilter Filter) : IRequest<FileContent>;

internal sealed class ExportBlNotesHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportBlNotesQuery, FileContent>
{
    private static readonly Dictionary<string, string> States = new()
    {
        ["draft"] = "Créé", ["awaiting-receipt"] = "En attente de réception", ["received"] = "Réceptionné", ["returned"] = "Remis au vendeur", ["closed"] = "Clôturé",
    };

    private static readonly IReadOnlyList<ExportColumn<BlNoteListItemDto>> Columns =
    [
        new("Bon BL", r => r.Code, Text: true),
        new("État", r => States[r.Status]),
        new("Client Pro", r => r.ClientName),
        new("Livreur", r => r.CourierName),
        new("Agence", r => r.AgencyName),
        new("BL", r => r.SlipCount),
        new("Remis à l’agence le", r => Exports.DateTimeText(r.HandedOverAt)),
        new("Réceptionné le", r => Exports.DateTimeText(r.ReceivedAt)),
        new("Remis au vendeur le", r => Exports.DateTimeText(r.ReturnedAt)),
        new("Remis à", r => r.ReturnedToName ?? string.Empty),
        new("Clôturé le", r => Exports.DateTimeText(r.ClosedAt)),
        new("Créé le", r => Exports.DateTimeText(r.CreatedAt)),
        new("Créé par", r => r.CreatedByName ?? string.Empty),
    ];

    public async Task<FileContent> Handle(ExportBlNotesQuery request, CancellationToken cancellationToken)
    {
        BlNoteLists.Validate(request.Filter);
        var where = BlNoteLists.Filter(db, caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Bons BL", Columns, await BlRows.ProjectNotes(where).ToListAsync(cancellationToken), cancellationToken);
    }
}

/// <summary>`GET /bl/notes/:code`.</summary>
public sealed record GetBlNoteQuery(string Code) : IRequest<BlNoteDetailDto>;

internal sealed class GetBlNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetBlNoteQuery, BlNoteDetailDto>
{
    public Task<BlNoteDetailDto> Handle(GetBlNoteQuery request, CancellationToken cancellationToken) => BlRows.NoteDetailAsync(db, caller, request.Code, cancellationToken);
}

/// <summary>`blNoteSchema` : un bon BL brouillon d'un Client Pro, avec ses premiers BL.</summary>
public sealed record CreateBlNoteCommand : IRequest<BlNoteDetailDto>
{
    public string? ClientId { get; init; }
    public string? AgencyId { get; init; }
    public IReadOnlyList<string>? SlipIds { get; init; }
    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class CreateBlNoteValidator : ZodValidator<CreateBlNoteCommand>
{
    public CreateBlNoteValidator()
    {
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.Note).MaxLength(500);
        RuleFor(x => x.SlipIds).Custom((ids, context) =>
        {
            if (ids is null) return;
            if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + "slipIds", "too-many");
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"slipIds.{i}", Zod.TooSmallString(1));
        });
    }
}

internal sealed class CreateBlNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateBlNoteCommand, BlNoteDetailDto>
{
    public async Task<BlNoteDetailDto> Handle(CreateBlNoteCommand input, CancellationToken cancellationToken)
    {
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == input.ClientId && c.DeletedAt == null).Select(c => new { c.Id, c.IsPro }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound("E-commerçant introuvable.");
        if (!client.IsPro) throw BusinessException.Conflict("Cet e-commerçant n'est pas un Client Pro.", new { reason = "client-not-pro" });
        if (!await db.Agencies.AnyAsync(a => a.Id == input.AgencyId && a.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("Agence introuvable.");
        var slipIds = (input.SlipIds ?? []).Distinct().ToList();
        if (slipIds.Count == 0) throw BusinessException.Validation("Un bon BL sans BL ne s’ouvre pas.", new { field = "slipIds", reason = "bl-note-empty" });
        var courierId = caller.CourierId ?? await db.DeliverySlips.AsNoTracking().Where(s => s.ClientId == input.ClientId && s.Status == DeliverySlipStatus.COLLECTED && s.CourierId != null)
            .OrderBy(s => s.CollectedAt).Select(s => s.CourierId).FirstOrDefaultAsync(cancellationToken);
        if (courierId is null) throw BusinessException.Validation("Aucun livreur ne porte de BL pour cet e-commerçant.");
        var now = DateTime.UtcNow;
        string code;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            code = await DocumentCodes.ReserveAsync(db, BlRows.NotePrefix, now, cancellationToken);
            var note = new BlNote
            {
                Code = code, Status = BlNoteStatus.DRAFT, CourierId = courierId, ClientId = input.ClientId!, AgencyId = input.AgencyId!, Note = string.IsNullOrEmpty(input.Note) ? null : input.Note,
                CreatedById = caller.UserId, CreatedAt = now,
            };
            db.BlNotes.Add(note);
            db.BlNoteEvents.Add(new BlNoteEvent { NoteId = note.Id, Status = BlNoteStatus.DRAFT, Comment = "Bon BL ouvert", AuthorId = caller.UserId, CreatedAt = now });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        await BlNoteSlips.AddAsync(db, caller, code, slipIds, cancellationToken);
        return await BlRows.NoteDetailAsync(db, caller, code, cancellationToken);
    }
}

internal static class BlNoteSlips
{
    public static async Task AddAsync(IAppDbContext db, ICurrentUser caller, string code, IReadOnlyList<string> slipIds, CancellationToken cancellationToken)
    {
        var note = await BlRows.Notes(db.BlNotes.AsNoTracking(), caller).Where(n => n.Code == code).Select(n => new { n.Id, n.Status, n.ClientId, n.CourierId }).FirstOrDefaultAsync(cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon BL introuvable : {code}");
        if (note.Status != BlNoteStatus.DRAFT) throw BusinessException.Conflict("Ce bon BL ne se modifie plus.", new { reason = "bl-note-locked" });
        foreach (var slipId in slipIds.Distinct())
        {
            var slip = await db.DeliverySlips.AsNoTracking().Where(s => s.Id == slipId).Select(s => new { s.Id, s.ClientId, s.CourierId, s.Status, s.Parcel.Code }).FirstOrDefaultAsync(cancellationToken)
                       ?? throw BusinessException.NotFound("BL introuvable.");
            if (slip.Status != DeliverySlipStatus.COLLECTED) throw BusinessException.Conflict($"Le BL du colis {slip.Code} a déjà quitté la sacoche.", new { reason = "bl-not-collected" });
            if (slip.ClientId != note.ClientId) throw BusinessException.Conflict($"Le BL du colis {slip.Code} appartient à un autre e-commerçant.", new { reason = "bl-other-client" });
            if (slip.CourierId != note.CourierId) throw BusinessException.Conflict($"Le BL du colis {slip.Code} a été récupéré par un autre livreur.", new { reason = "bl-other-courier" });
            try
            {
                db.BlNoteItems.Add(new BlNoteItem { NoteId = note.Id, SlipId = slip.Id, ReservedSlipId = slip.Id });
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                throw BusinessException.Conflict($"Le BL du colis {slip.Code} est déjà sur un bon BL.", new { reason = "bl-already-reserved" });
            }
        }
    }
}

/// <summary>`blNoteItemsSchema`.</summary>
public sealed record AddBlNoteSlipsCommand : IRequest<BlNoteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public IReadOnlyList<string>? SlipIds { get; init; }
}

public sealed class AddBlNoteSlipsValidator : ZodValidator<AddBlNoteSlipsCommand>
{
    public AddBlNoteSlipsValidator()
    {
        RuleFor(x => x.SlipIds).Required("array").Custom((ids, context) =>
        {
            if (ids is null) return;
            if (ids.Count < 1) context.AddFailure(ValidationPaths.Raw + "slipIds", "required");
            if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + "slipIds", "too-many");
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"slipIds.{i}", Zod.TooSmallString(1));
        });
    }
}

internal sealed class AddBlNoteSlipsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddBlNoteSlipsCommand, BlNoteDetailDto>
{
    public async Task<BlNoteDetailDto> Handle(AddBlNoteSlipsCommand input, CancellationToken cancellationToken)
    {
        await BlNoteSlips.AddAsync(db, caller, input.Code, input.SlipIds!, cancellationToken);
        return await BlRows.NoteDetailAsync(db, caller, input.Code, cancellationToken);
    }
}

/// <summary>`DELETE /bl/notes/:code/slips/:slipId`.</summary>
public sealed record RemoveBlNoteSlipCommand(string Code, string SlipId) : IRequest<BlNoteDetailDto>;

internal sealed class RemoveBlNoteSlipHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveBlNoteSlipCommand, BlNoteDetailDto>
{
    public async Task<BlNoteDetailDto> Handle(RemoveBlNoteSlipCommand request, CancellationToken cancellationToken)
    {
        var note = await BlRows.Notes(db.BlNotes.AsNoTracking(), caller).Where(n => n.Code == request.Code).Select(n => new { n.Id, n.Status }).FirstOrDefaultAsync(cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon BL introuvable : {request.Code}");
        if (note.Status != BlNoteStatus.DRAFT) throw BusinessException.Conflict("Ce bon BL ne se modifie plus.", new { reason = "bl-note-locked" });
        await db.BlNoteItems.Where(i => i.NoteId == note.Id && i.SlipId == request.SlipId).ExecuteDeleteAsync(cancellationToken);
        return await BlRows.NoteDetailAsync(db, caller, request.Code, cancellationToken);
    }
}

/// <summary>Les quatre étapes d'un bon BL (`advanceNote`) : remis, reçu, rendu au vendeur, clôturé.</summary>
public sealed record AdvanceBlNoteCommand : IRequest<BlNoteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    [JsonIgnore]
    public string Step { get; init; } = string.Empty;

    public string? ReturnedToName { get; init => field = Zod.Trim(value); }
}

public sealed class AdvanceBlNoteValidator : ZodValidator<AdvanceBlNoteCommand>
{
    public AdvanceBlNoteValidator()
    {
        RuleFor(x => x.ReturnedToName).Required().MinLength(2, "too-short").MaxLength(120).When(x => x.Step == "return");
    }
}

internal sealed class AdvanceBlNoteHandler(IAppDbContext db, ICurrentUser caller, ILogger<AdvanceBlNoteHandler> logger) : IRequestHandler<AdvanceBlNoteCommand, BlNoteDetailDto>
{
    private sealed record Step(BlNoteStatus From, BlNoteStatus To, DeliverySlipStatus SlipStatus, string Comment, string Refusal);

    public async Task<BlNoteDetailDto> Handle(AdvanceBlNoteCommand input, CancellationToken cancellationToken)
    {
        var shortName = await BlRows.ShortNameAsync(db, cancellationToken);
        var step = input.Step switch
        {
            "handover" => new Step(BlNoteStatus.DRAFT, BlNoteStatus.AWAITING_RECEIPT, DeliverySlipStatus.HANDED_OVER, "Liasse remise à l’entrepôt", "Ce bon BL a déjà quitté la sacoche."),
            "receive" => new Step(BlNoteStatus.AWAITING_RECEIPT, BlNoteStatus.RECEIVED, DeliverySlipStatus.RECEIVED, $"Réception confirmée par {shortName}",
                "Ce bon BL doit être remis à l’entrepôt avant d’être reçu."),
            "return" => new Step(BlNoteStatus.RECEIVED, BlNoteStatus.RETURNED, DeliverySlipStatus.RETURNED, $"Liasse remise à {input.ReturnedToName}",
                "La réception de ce bon BL n’est pas confirmée."),
            _ => new Step(BlNoteStatus.RETURNED, BlNoteStatus.CLOSED, DeliverySlipStatus.CLOSED, "Bon BL clôturé", "Seul un bon BL remis au vendeur se clôture."),
        };
        var note = await BlRows.Notes(db.BlNotes, caller).FirstOrDefaultAsync(n => n.Code == input.Code, cancellationToken) ?? throw BusinessException.NotFound($"Bon BL introuvable : {input.Code}");
        if (note.Status != step.From) throw BusinessException.Conflict(step.Refusal, new { reason = "bl-note-wrong-status" });
        var slipIds = await db.BlNoteItems.Where(i => i.NoteId == note.Id).Select(i => i.SlipId).ToListAsync(cancellationToken);
        if (slipIds.Count == 0) throw BusinessException.Validation("Un bon BL sans BL ne se valide pas.");
        var now = DateTime.UtcNow;
        List<BlRows.Moved> moved;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            note.Status = step.To;
            switch (step.To)
            {
                case BlNoteStatus.AWAITING_RECEIPT: note.HandedOverAt = now; note.HandedOverById = caller.UserId; break;
                case BlNoteStatus.RECEIVED: note.ReceivedAt = now; note.ReceivedById = caller.UserId; break;
                case BlNoteStatus.RETURNED: note.ReturnedAt = now; note.ReturnedById = caller.UserId; note.ReturnedToName = input.ReturnedToName; break;
                default: note.ClosedAt = now; note.ClosedById = caller.UserId; break;
            }
            db.BlNoteEvents.Add(new BlNoteEvent { NoteId = note.Id, Status = step.To, Comment = step.Comment, AuthorId = caller.UserId, CreatedAt = now });
            await db.SaveChangesAsync(cancellationToken);
            moved = await BlRows.AdvanceSlipsAsync(db, caller, db.DeliverySlips.Where(s => slipIds.Contains(s.Id)), step.SlipStatus, step.Comment, $"{step.Comment} — bon BL {note.Code}", now,
                input.ReturnedToName, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        db.ChangeTracker.Clear();
        var detail = await BlRows.NoteDetailAsync(db, caller, input.Code, cancellationToken);
        if (step.To == BlNoteStatus.AWAITING_RECEIPT)
        {
            try
            {
                await Notifications.Notifier.NotifyStaffAsync(db, detail.AgencyId, "bl-note-awaiting", "returns.confirm",
                    new Dictionary<string, object> { ["code"] = detail.Code, ["count"] = detail.SlipCount },
                    new Dictionary<string, string> { ["topic"] = "bl-note-awaiting", ["blNoteCode"] = detail.Code }, cancellationToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning("Notification « bl-note-awaiting » non envoyée : {Message}", error.Message);
            }
        }
        await BlRows.NotifySlipStepAsync(db, logger, moved, step.SlipStatus, detail.Code, shortName, cancellationToken);
        return detail;
    }
}

/// <summary>`DELETE /bl/notes/:code` : un brouillon seulement.</summary>
public sealed record DeleteBlNoteCommand(string Code) : IRequest<BlNoteCodeDto>;

public sealed record BlNoteCodeDto(string Code);

internal sealed class DeleteBlNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<DeleteBlNoteCommand, BlNoteCodeDto>
{
    public async Task<BlNoteCodeDto> Handle(DeleteBlNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await BlRows.Notes(db.BlNotes, caller).FirstOrDefaultAsync(n => n.Code == request.Code, cancellationToken) ?? throw BusinessException.NotFound($"Bon BL introuvable : {request.Code}");
        if (note.Status != BlNoteStatus.DRAFT) throw BusinessException.Conflict("Ce bon BL a quitté la sacoche : il ne se supprime plus.", new { reason = "bl-note-locked" });
        db.BlNotes.Remove(note);
        await db.SaveChangesAsync(cancellationToken);
        return new BlNoteCodeDto(request.Code);
    }
}

/// <summary>`GET /bl/notes/:code/pdf` : le bon imprimé par un vendeur dit « Livreur &lt;nom court&gt; ».</summary>
public sealed record GetBlNotePdfQuery(string Code) : IRequest<FileContent>;

internal sealed class GetBlNotePdfHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf) : IRequestHandler<GetBlNotePdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetBlNotePdfQuery request, CancellationToken cancellationToken)
    {
        var note = await CourierMasks.ForReaderAsync(db, caller, await BlRows.NoteDetailAsync(db, caller, request.Code, cancellationToken), cancellationToken);
        var company = await PdfCompany.LoadAsync(db, "Bon de retour BL", cancellationToken);
        var sheet = new BlNoteSheet(company, note.Code, note.Status, note.AgencyName, note.CreatedAt, note.ClientName, note.CourierName, note.ReceivedAt, note.HandedOverAt, note.ReturnedToName,
            note.SlipCount, note.Items.Select(i => new BlNoteSheetLine(i.ParcelCode, i.RecipientName, i.Reference)).ToList());
        return new FileContent(pdf.Render(sheet), FileContent.Pdf, $"{note.Code}.pdf", CacheControl: "private, no-store");
    }
}
