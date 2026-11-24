using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Pricing;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.DeliveryNotes;

/// <summary>`DeliveryNoteListItem`.</summary>
public class DeliveryNoteListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string LocationCityId { get; set; } = string.Empty;
    public List<string> ParcelIds { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public int ParcelCount { get; set; }
    public int PickedCount { get; set; }
    public int RemovedCount { get; set; }
    public bool PickupWaived { get; set; }
}

/// <summary>`NoteParcelClientCorrection`.</summary>
public sealed record NoteClientCorrectionDto(DateTime At, string? ByName, string? FromClientName, string? Reason);

/// <summary>`NoteParcel` : une ligne du bon.</summary>
public sealed class NoteParcelDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string DestinationCityName { get; set; } = string.Empty;
    public string? DestinationDistrictName { get; set; }
    public string Description { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public long CodAmount { get; set; }
    public long DeliveryFee { get; set; }
    public DateTime ParcelCreatedAt { get; set; }
    public int Quantity { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ClientCorrected { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?> Extra { get; set; } = [];

    public NoteClientCorrectionDto? ClientCorrection { get; set; }
}

public sealed record RemovedNoteParcelDto(string Id, string Code, string RecipientName, int StatusId, DateTime RemovedAt, string? RemovedByName, DateTime? RestoredAt, string Reason, bool Restorable);

public sealed record DepartedNoteParcelDto(string Code, string RecipientName, string Kind, DateTime At, string? ByName, string? Reason);

public sealed record PickupRuleDto(long Fee, long FreeFrom);

/// <summary>`DeliveryNoteDetail`.</summary>
public sealed class DeliveryNoteDetailDto : DeliveryNoteListItemDto
{
    [JsonPropertyOrder(1)] public List<NoteParcelDto> Parcels { get; set; } = [];
    [JsonPropertyOrder(1)] public List<RemovedNoteParcelDto> RemovedParcels { get; set; } = [];
    [JsonPropertyOrder(1)] public List<DepartedNoteParcelDto> DepartedParcels { get; set; } = [];
    [JsonPropertyOrder(1)] public bool Validatable { get; set; }
    [JsonPropertyOrder(1)] public bool Correctable { get; set; }
    [JsonPropertyOrder(1)] public long? PickupFeeCharged { get; set; }
    [JsonPropertyOrder(1)] public PickupRuleDto PickupRule { get; set; } = new(0, 0);
}

/// <summary>`noteListQuerySchema` une fois lu.</summary>
public sealed record NoteListFilter(ListQuery Paging, string? Kind, IReadOnlyList<string>? Status, IReadOnlyList<string>? ClientId);

public static class NoteRows
{
    /// <summary>`noteWhere` : le vendeur ses bons, l'agent son agence ; le livreur n'est pas borné ici (Speedy ignore sa clé sur les bons).</summary>
    public static IQueryable<DeliveryNote> Scoped(IQueryable<DeliveryNote> notes, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return notes;
        if (caller.ClientId is { } clientId) return notes.Where(n => n.ClientId == clientId);
        if (caller.CourierId is not null) return notes;
        if (caller.AgencyId is { } agencyId) return notes.Where(n => n.AgencyId == agencyId);
        return notes.Where(n => n.Id == Scope.DenyAllId);
    }

    public static IQueryable<DeliveryNote> Filter(IQueryable<DeliveryNote> notes, ICurrentUser caller, NoteListFilter filter)
    {
        var where = Scoped(notes, caller);
        if (!string.IsNullOrEmpty(filter.Kind)) { var kind = Enum.Parse<DeliveryNoteType>(Money.FromWire(filter.Kind)); where = where.Where(n => n.Type == kind); }
        if (filter.Status is { Count: > 0 } statuses)
        {
            var wanted = statuses.Select(s => Enum.Parse<DeliveryNoteStatus>(Money.FromWire(s))).ToList();
            where = where.Where(n => wanted.Contains(n.Status));
        }
        if (filter.ClientId is { Count: > 0 } clients && caller.MayFilterBy(caller.ClientId)) where = where.Where(n => clients.Contains(n.ClientId));
        if (!string.IsNullOrWhiteSpace(filter.Paging.Search))
        {
            var pattern = Like.Contains(filter.Paging.Search.Trim());
            where = where.Where(n => EF.Functions.ILike(n.Code, pattern) || EF.Functions.ILike(n.Client.Name, pattern) || EF.Functions.ILike(n.LocationCity.Name, pattern));
        }
        return where;
    }

    internal sealed record Row(string Id, string Code, DeliveryNoteType Type, DeliveryNoteStatus Status, string ClientId, string AgencyId, string LocationCityId, DateTime CreatedAt, DateTime? ValidatedAt,
        string ClientName, decimal? ClientPickupFee, int? ClientThreshold, decimal? FeeApplied, int? ThresholdApplied, string CityName, List<(string ParcelId, int StatusId)> Parcels, int RemovedCount);

    internal static async Task<List<Row>> ProjectAsync(IQueryable<DeliveryNote> notes, CancellationToken cancellationToken) =>
        (await notes.Select(n => new
        {
            n.Id, n.Code, n.Type, n.Status, n.ClientId, n.AgencyId, n.LocationCityId, n.CreatedAt, n.ValidatedAt, ClientName = n.Client.Name, n.Client.PickupFee, n.Client.FreePickupThreshold,
            n.PickupFeeApplied, n.PickupThresholdApplied, CityName = n.LocationCity.Name,
            Parcels = n.Parcels.Select(l => new { l.ParcelId, l.Parcel.StatusId }).ToList(),
            Removed = n.RemovedParcels.Count,
        }).ToListAsync(cancellationToken))
        .Select(n => new Row(n.Id, n.Code, n.Type, n.Status, n.ClientId, n.AgencyId, n.LocationCityId, n.CreatedAt, n.ValidatedAt, n.ClientName, n.PickupFee, n.FreePickupThreshold,
            n.PickupFeeApplied, n.PickupThresholdApplied, n.CityName, n.Parcels.Select(p => (p.ParcelId, p.StatusId)).ToList(), n.Removed)).ToList();

    internal static T Fill<T>(T dto, Row r, PricingPolicy policy)
        where T : DeliveryNoteListItemDto
    {
        var picked = r.Status == DeliveryNoteStatus.VALIDATED ? r.Parcels.Count : r.Parcels.Count(p => p.StatusId is NoteRules.PickedUpByCourier or NoteRules.PickedUp);
        dto.Id = r.Id;
        dto.Code = r.Code;
        dto.Kind = Money.ToWire(r.Type);
        dto.Status = Money.ToWire(r.Status);
        dto.ClientId = r.ClientId;
        dto.AgencyId = r.AgencyId;
        dto.LocationCityId = r.LocationCityId;
        dto.ParcelIds = r.Parcels.Select(p => p.ParcelId).ToList();
        dto.CreatedAt = r.CreatedAt;
        dto.ValidatedAt = r.ValidatedAt;
        dto.ClientName = r.ClientName;
        dto.CityName = r.CityName;
        dto.ParcelCount = r.Parcels.Count;
        dto.PickedCount = picked;
        dto.RemovedCount = r.RemovedCount;
        dto.PickupWaived = r.Type == DeliveryNoteType.PICKUP && r.Parcels.Count >= Rule(r, policy).FreeFrom;
        return dto;
    }

    internal static (long Fee, long FreeFrom) Rule(Row r, PricingPolicy policy) =>
        NoteRules.RuleOf(r.Status, r.FeeApplied, r.ThresholdApplied, r.ClientPickupFee, r.ClientThreshold, policy);

    public static async Task<DeliveryNoteDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var row = (await ProjectAsync(Scoped(db.DeliveryNotes.AsNoTracking(), caller).Where(n => n.Code == code), cancellationToken)).FirstOrDefault()
                  ?? throw BusinessException.NotFound($"Bon introuvable : {code}");
        var merchant = caller.ClientId is not null;
        var lines = await db.DeliveryNoteParcels.AsNoTracking().Where(l => l.DeliveryNoteId == row.Id)
            .Select(l => new
            {
                l.Parcel.Id, l.Parcel.Code, l.Parcel.RecipientName, City = l.Parcel.DestinationCity.Name, l.Parcel.DestinationDistrictName,
                DistrictRef = l.Parcel.DestinationDistrict != null ? l.Parcel.DestinationDistrict.Name : null, l.Parcel.Description, l.Parcel.StatusId,
                l.Parcel.CodAmount, l.Parcel.DeliveryFee, l.Parcel.CreatedAt, l.Parcel.Quantity, l.Parcel.ClientId, ClientName = l.Parcel.Client.Name,
            }).ToListAsync(cancellationToken);
        var removedRows = await db.DeliveryNoteRemovedParcels.AsNoTracking().Where(r => r.DeliveryNoteId == row.Id).OrderBy(r => r.RemovedAt)
            .Select(r => new { r.ParcelId, r.Parcel.Code, r.Parcel.RecipientName, r.Parcel.StatusId, r.Parcel.DeletedAt, r.RemovedAt, r.RemovedBy, r.RestoredAt, r.RouteId })
            .ToListAsync(cancellationToken);
        var removerIds = merchant ? [] : removedRows.Select(r => r.RemovedBy).OfType<string>().Distinct().ToList();
        var removers = (await db.Users.AsNoTracking().Where(u => removerIds.Contains(u.Id)).Select(u => new { u.Id, u.FirstName, u.LastName, u.Email }).ToListAsync(cancellationToken))
            .ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim() is { Length: > 0 } n ? n : u.Email);
        var routeIds = removedRows.Select(r => r.RouteId).OfType<string>().Distinct().ToList();
        var closed = (await db.Routes.AsNoTracking().Where(r => routeIds.Contains(r.Id) && r.Status == RouteStatus.CLOSED).Select(r => r.Id).ToListAsync(cancellationToken)).ToHashSet();
        var policy = await NoteRules.PolicyAsync(db, row.AgencyId, cancellationToken);
        var (corrections, departed) = await TraceAsync(db, caller, row.Code, lines.Select(l => l.Id).ToList(), removedRows.Select(r => r.ParcelId).ToHashSet(), cancellationToken);
        var detail = Fill(new DeliveryNoteDetailDto(), row, policy);
        detail.Parcels = lines.Select(l =>
        {
            var line = new NoteParcelDto
            {
                Id = l.Id, Code = l.Code, RecipientName = l.RecipientName, DestinationCityName = l.City, DestinationDistrictName = ParcelViews.DistrictLabel(l.DestinationDistrictName, l.DistrictRef),
                Description = l.Description, StatusId = l.StatusId, CodAmount = Money.ToCents(l.CodAmount), DeliveryFee = Money.ToCents(l.DeliveryFee), ParcelCreatedAt = l.CreatedAt,
                Quantity = l.Quantity, ClientCorrection = corrections.GetValueOrDefault(l.Id),
            };
            if (l.ClientId != row.ClientId)
            {
                line.ClientCorrected = true;
                line.Extra["correctedClientName"] = merchant ? null : l.ClientName;
            }
            return line;
        }).ToList();
        detail.RemovedParcels = removedRows.Select(r => new RemovedNoteParcelDto(r.ParcelId, r.Code, r.RecipientName, r.StatusId, r.RemovedAt,
            r.RemovedBy is null ? null : removers.GetValueOrDefault(r.RemovedBy), r.RestoredAt,
            r.DeletedAt is not null ? NoteRules.LegacyRemovalReason(row.Code) : NoteRules.RemovalReason(row.Code),
            r.RestoredAt is null && r.DeletedAt is not null && row.Status != DeliveryNoteStatus.VALIDATED && !(r.RouteId is not null && closed.Contains(r.RouteId)))).ToList();
        detail.DepartedParcels = departed;
        detail.Validatable = row.Status != DeliveryNoteStatus.VALIDATED && lines.Count > 0;
        detail.Correctable = row.Status == DeliveryNoteStatus.VALIDATED && lines.Count > 1 && (await NoteRules.OpenRoutesOfNoteAsync(db, row.Id, cancellationToken)).Count > 0;
        var charged = await db.InvoiceAdjustments.AsNoTracking().Where(a => a.DeliveryNoteId == row.Id).Select(a => (decimal?)a.Amount).FirstOrDefaultAsync(cancellationToken);
        detail.PickupFeeCharged = Money.ToCentsOrNull(charged);
        var (fee, freeFrom) = Rule(row, policy);
        detail.PickupRule = new PickupRuleDto(fee, freeFrom);
        return detail;
    }

    /// <summary>`noteTrace` : les corrections de client des lignes et les colis partis du bon.</summary>
    private static async Task<(Dictionary<string, NoteClientCorrectionDto> Corrections, List<DepartedNoteParcelDto> Departed)> TraceAsync(IAppDbContext db, ICurrentUser caller, string noteCode,
        List<string> lineIds, HashSet<string> removedIds, CancellationToken cancellationToken)
    {
        var merchant = caller.ClientId is not null;
        var changes = lineIds.Count == 0 ? [] : await db.FieldChanges.AsNoTracking().Where(c => c.Entity == "Parcel" && lineIds.Contains(c.EntityId) && c.Field == "clientId")
            .OrderByDescending(c => c.At).ToListAsync(cancellationToken);
        var departures = await db.FieldChanges.AsNoTracking().Where(c => c.Entity == "Parcel" && c.Field == ParcelJournal.NoteDepartureField && c.OldValue == noteCode && c.NewValue == string.Empty)
            .OrderBy(c => c.At).ToListAsync(cancellationToken);
        var latest = new Dictionary<string, FieldChange>();
        foreach (var change in changes) latest.TryAdd(change.EntityId, change);
        static string? KindOf(string? reason) => reason?.StartsWith(ParcelJournal.ClientChangePrefix, StringComparison.Ordinal) == true ? "client-change"
            : reason?.StartsWith(ParcelJournal.DeletedPrefix, StringComparison.Ordinal) == true ? "deleted" : null;
        var gone = departures.Where(d => KindOf(d.Reason) is not null && !lineIds.Contains(d.EntityId) && !removedIds.Contains(d.EntityId)).ToList();
        var clientIds = latest.Values.Select(c => c.OldValue).OfType<string>().ToList();
        var clientNames = await db.Clients.AsNoTracking().Where(c => clientIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var goneIds = gone.Select(g => g.EntityId).ToList();
        var parcels = await db.Parcels.AsNoTracking().Where(p => goneIds.Contains(p.Id)).Select(p => new { p.Id, p.Code, p.RecipientName }).ToDictionaryAsync(p => p.Id, cancellationToken);
        var corrections = latest.ToDictionary(e => e.Key, e => new NoteClientCorrectionDto(e.Value.At, merchant ? null : e.Value.AuthorName,
            merchant && e.Value.OldValue != caller.ClientId ? null : clientNames.GetValueOrDefault(e.Value.OldValue ?? string.Empty), merchant ? null : e.Value.Reason));
        var departed = new List<DepartedNoteParcelDto>();
        var seen = new HashSet<string>();
        foreach (var row in gone)
        {
            if (!parcels.TryGetValue(row.EntityId, out var parcel) || !seen.Add(row.EntityId)) continue;
            var kind = KindOf(row.Reason)!;
            var prefix = kind == "deleted" ? ParcelJournal.DeletedPrefix : ParcelJournal.ClientChangePrefix;
            var reason = (row.Reason ?? string.Empty)[Math.Min(prefix.Length, (row.Reason ?? string.Empty).Length)..];
            departed.Add(new DepartedNoteParcelDto(parcel.Code, parcel.RecipientName, kind, row.At, merchant ? null : row.AuthorName, merchant ? null : reason.Length == 0 ? null : reason));
        }
        return (corrections, departed);
    }

    /// <summary>`clientCorrectionText`.</summary>
    public static string ClientCorrectionText(NoteClientCorrectionDto? correction)
    {
        if (correction is null) return string.Empty;
        var by = correction.ByName is null ? string.Empty : $" par {correction.ByName}";
        var before = correction.FromClientName is null ? string.Empty : $", avant : {correction.FromClientName}";
        return $"Client corrigé le {Common.Export.Exports.DateTimeText(correction.At)}{by}{before}";
    }
}
