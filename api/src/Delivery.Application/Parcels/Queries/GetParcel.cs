using System.Globalization;
using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Parcels.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Parcels.Queries;

/// <summary>`GET /parcels/:code` : la fiche d'un colis (code ou référence vendeur).</summary>
public sealed record GetParcelQuery(string Code, bool IncludeDeleted = false) : IRequest<ParcelDetailDto>;

internal sealed class GetParcelHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetParcelQuery, ParcelDetailDto>
{
    /// <summary>`PARCEL_TRACE_NAMED_FIELDS` : les champs du journal qui portent un identifiant.</summary>
    internal static readonly string[] LabelledFields = ["destinationDistrictId", "destinationCityId", "originCityId", "clientId", "agencyId", "courierId"];

    internal static readonly string[] FeeFields = ["codAmount", "deliveryFee", "failureFee", "otherFees"];

    public async Task<ParcelDetailDto> Handle(GetParcelQuery request, CancellationToken cancellationToken)
    {
        var scoped = request.IncludeDeleted
            ? ParcelFilters.ScopedIncludingDeleted(db.Parcels.AsNoTracking(), caller)
            : ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller);
        var row = await ParcelLookup.ResolveAsync(scoped, request.Code, q => mapper.ProjectTo<ParcelRow>(q).FirstOrDefaultAsync(cancellationToken))
                  ?? throw BusinessException.NotFound($"Colis introuvable : {request.Code}");
        var id = row.Id;
        var internalView = caller.CanSeeInternalNotes();
        var internalCash = caller.CanSeeInternalCash();

        var agencyName = await db.Agencies.AsNoTracking().Where(a => a.Id == row.AgencyId).Select(a => a.Name).FirstAsync(cancellationToken);
        var events = await db.ParcelEvents.AsNoTracking().Where(e => e.ParcelId == id).OrderByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                e.Id, e.ParcelId, e.StatusId, e.Comment, e.AuthorName, e.CityId, e.AttachmentName, e.ScheduledDate, e.CreatedAt,
                AuthorFirst = e.Author != null ? e.Author.FirstName : null,
                AuthorLast = e.Author != null ? e.Author.LastName : null,
                AuthorIsCourier = e.Author != null && e.Author.Courier != null,
            }).ToListAsync(cancellationToken);
        var notes = internalView
            ? await db.ParcelNotes.AsNoTracking().Where(n => n.ParcelId == id).OrderByDescending(n => n.CreatedAt)
                .Select(n => new ParcelNoteDto(n.Id, n.ParcelId, n.Body, n.AuthorName, n.AttachmentName, n.CreatedAt)).ToListAsync(cancellationToken)
            : [];
        var journal = await db.FieldChanges.AsNoTracking().Where(c => c.Entity == "Parcel" && c.EntityId == id)
            .OrderByDescending(c => c.At).Take(100).ToListAsync(cancellationToken);
        var labels = await JournalLabelsAsync(db, journal.Where(c => LabelledFields.Contains(c.Field)).SelectMany(c => new[] { c.OldValue, c.NewValue }), cancellationToken);

        var originId = row.Kind == ParcelKind.RETURN && row.ReferenceParcelId is not null ? row.ReferenceParcelId : id;
        var exchanges = await db.Exchanges.AsNoTracking()
            .Where(x => x.OriginalParcelId == originId || x.NewParcelId == id)
            .OrderByDescending(x => x.RequestedAt).Take(20)
            .Select(x => new ExchangeLinkRow(x.Id, x.Code, x.Status, x.OriginalParcelId, x.ReturnedProduct, x.NewProduct, x.RequestedAt, x.OriginalParcel.Code,
                x.OriginalParcel.DerivedParcels.Where(d => d.Kind == ParcelKind.RETURN && d.DeletedAt == null).Select(d => d.Code).FirstOrDefault(),
                x.NewParcel != null ? x.NewParcel.Code : null))
            .ToListAsync(cancellationToken);

        var links = await db.RouteParcels.AsNoTracking().Where(l => l.ParcelId == id).OrderByDescending(l => l.Route.CreatedAt).Take(20)
            .Select(l => new RouteLinkRow(l.Route.Id, l.Route.Code, l.Route.Kind, l.Route.Status, l.Route.Courier.FirstName + " " + l.Route.Courier.LastName,
                l.Route.City.Name, l.Route.ExitDate, l.Confirmed, l.ConfirmedAt, l.FinalStatusId, l.CodCollected, l.CodCash, l.CodCheque, l.CourierCommission, l.CourierCommissionSource))
            .ToListAsync(cancellationToken);
        var line = await db.InvoiceLines.AsNoTracking().Where(l => l.ParcelId == id && l.InvoicedParcelId != null)
            .Select(l => new { l.NetDue, l.Invoice.Code, l.Invoice.Status, l.Invoice.IssuedAt, l.Invoice.PaidAt }).FirstOrDefaultAsync(cancellationToken);
        var items = await db.ParcelItems.AsNoTracking().Where(i => i.ParcelId == id).OrderBy(i => i.ProductId)
            .Select(i => new ParcelItemDto(i.ProductId, i.Product.Name, i.Product.Reference, i.Quantity)).ToListAsync(cancellationToken);

        var policy = await db.PricingPolicyAsync(cancellationToken, row.AgencyId);
        var detail = ParcelViews.Fill(new ParcelDetailDto(), row, caller);
        ParcelViews.Label(detail, row);
        detail.DeletedAt = row.DeletedAt;
        detail.ReturnFee = row.OriginCityId == row.DestinationCityId ? policy.FailureFeeSameCity : policy.FailureFeeCrossCity;
        detail.AgencyName = agencyName;
        detail.CodToCollect = FinancialStatuses.CodToCollect(detail.CodAmount, detail.DeliveryFee, detail.FeeIncluded, detail.AlreadyPaid);
        var settled = Settled(links);
        detail.Collected = CollectedOf(row, settled);
        detail.Events = events.Select(e =>
        {
            var authorName = e.AuthorFirst is not null ? $"{e.AuthorFirst} {e.AuthorLast}".Trim() : e.AuthorName;
            return internalView
                ? (object)new ParcelEventDto(e.Id, e.ParcelId, e.StatusId, e.Comment, authorName, e.CityId, e.AttachmentName, e.ScheduledDate, e.CreatedAt)
                : new ParcelMerchantEventDto(e.Id, e.ParcelId, e.StatusId, null, e.AuthorIsCourier ? authorName : null, e.CityId, e.AttachmentName, e.ScheduledDate, e.CreatedAt, BlStepOf(e.Comment));
        }).ToList();
        detail.Notes = notes;
        detail.FeeChanges = journal.Where(c => FeeFields.Contains(c.Field)).Select(c => new ParcelFeeChangeDto(
            c.Id, id, c.Field, c.OldValue is null ? null : JsNumber(c.OldValue), JsNumber(c.NewValue), c.Reason ?? string.Empty, internalView ? c.AuthorName : string.Empty, c.At)).ToList();
        detail.Exchanges = exchanges.Select(x => ToExchangeLink(x, id, row.Kind)).ToList();
        detail.FieldChanges = (internalView ? journal : []).Select(c =>
        {
            var dto = new ParcelFieldChangeDto { Id = c.Id, Entity = c.Entity, EntityId = c.EntityId, Field = c.Field, OldValue = c.OldValue, NewValue = c.NewValue, Reason = c.Reason, AuthorName = c.AuthorName, At = c.At };
            if (LabelledFields.Contains(c.Field))
            {
                dto.Extra["oldLabel"] = string.IsNullOrEmpty(c.OldValue) ? null : labels.GetValueOrDefault(c.OldValue);
                dto.Extra["newLabel"] = string.IsNullOrEmpty(c.NewValue) ? null : labels.GetValueOrDefault(c.NewValue);
            }
            return dto;
        }).ToList();
        detail.Routes = internalCash
            ? links.Select(l => new ParcelRouteLinkDto(l.RouteId, l.Code, Money.ToWire(l.Kind), Money.ToWire(l.Status), l.CourierName, l.CityName, l.ExitDate, l.Confirmed, l.ConfirmedAt, l.FinalStatusId)).ToList()
            : [];
        if (internalCash)
        {
            var cheque = await db.Cheques.AsNoTracking().Where(c => c.ParcelId == id).Select(c => new { c.Number, c.Status }).FirstOrDefaultAsync(cancellationToken);
            var remittance = await db.CashRemittanceParcels.AsNoTracking().Where(r => r.ParcelId == id)
                .Select(r => new { r.Remittance.Id, r.Remittance.Code, r.Remittance.Status }).FirstOrDefaultAsync(cancellationToken);
            var payout = await db.CodPayoutParcels.AsNoTracking().Where(p => p.ParcelId == id)
                .Select(p => new { p.Amount, p.Payout.Code, p.Payout.PaidAt }).FirstOrDefaultAsync(cancellationToken);
            detail.Finance = new ParcelFinanceViewDto(
                detail.CodToCollect,
                Money.ToCentsOrNull(settled?.CodCollected),
                Money.ToCentsOrNull(settled?.CodCash),
                Money.ToCentsOrNull(settled?.CodCheque),
                cheque?.Number,
                cheque is null ? null : Money.ToWire(cheque.Status),
                Money.ToCentsOrNull(settled?.CourierCommission),
                settled?.CommissionSource is { } source ? Money.ToWire(source) : null,
                remittance?.Id,
                remittance?.Code,
                remittance is null ? null : Money.ToWire(remittance.Status),
                line?.Code,
                line is null ? null : Money.ToWire(line.Status),
                line?.IssuedAt,
                line?.PaidAt,
                Money.ToCentsOrNull(line?.NetDue),
                payout?.Code,
                payout?.PaidAt,
                payout is { PaidAt: not null } ? Money.ToCents(payout.Amount) : line is { Status: InvoiceStatus.PAID } ? Money.ToCents(line.NetDue) : null);
        }
        detail.InvoicedOn = line?.Code;
        detail.InvoiceSettledOn = line is { Status: InvoiceStatus.PAID } ? line.Code : null;
        detail.Items = items;
        return detail;
    }

    internal sealed record ExchangeLinkRow(string Id, string Code, ExchangeStatus Status, string OriginalParcelId, string ReturnedProduct, string NewProduct, DateTime RequestedAt, string OriginalCode, string? ReturnCode, string? NewCode);

    internal sealed record RouteLinkRow(string RouteId, string Code, RouteKind Kind, RouteStatus Status, string CourierName, string CityName, DateTime? ExitDate, bool Confirmed, DateTime? ConfirmedAt, int? FinalStatusId,
        decimal? CodCollected, decimal? CodCash, decimal? CodCheque, decimal? CourierCommission, CourierCommissionSource? CommissionSource);

    /// <summary>`toExchangeLink`.</summary>
    internal static ParcelExchangeLinkDto ToExchangeLink(ExchangeLinkRow row, string viewerId, ParcelKind viewerKind)
    {
        var isOriginal = row.OriginalParcelId == viewerId;
        var role = viewerKind == ParcelKind.RETURN ? "return" : isOriginal ? "original" : "new";
        return new ParcelExchangeLinkDto(row.Id, row.Code, Money.ToWire(row.Status), role, isOriginal ? row.NewCode : row.OriginalCode,
            row.ReturnedProduct, row.NewProduct, row.RequestedAt, row.ReturnCode);
    }

    /// <summary>`settledRouteLink` : le dernier passage confirmé qui porte l'argent.</summary>
    internal static RouteLinkRow? Settled(IEnumerable<RouteLinkRow> links)
    {
        var confirmed = links.Where(l => l.Confirmed).OrderByDescending(l => l.ConfirmedAt ?? DateTime.MinValue).ToList();
        return confirmed.FirstOrDefault(l => l.CodCollected is not null) ?? confirmed.FirstOrDefault(l => l.Kind != RouteKind.PICKUP);
    }

    private static ParcelCollectedDto? CollectedOf(ParcelRow row, RouteLinkRow? settled)
    {
        if (row.StatusId != StatusCatalog.Delivered || row.FinancialStatus == FinancialStatus.NON_ENCAISSE) return null;
        if (Money.ToCentsOrNull(settled?.CodCollected) is not { } amount) return null;
        var cheque = Money.ToCentsOrNull(settled?.CodCheque) ?? 0;
        return new ParcelCollectedDto(amount, Money.ToCentsOrNull(settled?.CodCash) ?? amount - cheque, cheque);
    }

    /// <summary>`blParcelStepOf` : l'étape BL lisible dans le commentaire d'un événement.</summary>
    internal static string? BlStepOf(string? comment)
    {
        if (string.IsNullOrEmpty(comment)) return null;
        var text = comment.Trim();
        var ofNote = System.Text.RegularExpressions.Regex.IsMatch(text, @" — bon BL \S+$");
        if (text.StartsWith("BL récupéré par le livreur", StringComparison.Ordinal)) return "collected";
        if (text.StartsWith("BL — Retour arrière", StringComparison.Ordinal)) return "corrected";
        if (ofNote && text.StartsWith("Liasse remise à l’entrepôt", StringComparison.Ordinal)) return "handed-over";
        if (text.StartsWith("BL — Réceptionné par", StringComparison.Ordinal) || (ofNote && text.StartsWith("Réception confirmée par", StringComparison.Ordinal))) return "received";
        if (text.StartsWith("BL — Récupéré par le vendeur", StringComparison.Ordinal) || (ofNote && text.StartsWith("Liasse remise à", StringComparison.Ordinal))) return "returned";
        if (ofNote && text.StartsWith("Bon BL clôturé", StringComparison.Ordinal)) return "closed";
        return null;
    }

    /// <summary>`Number(text)` de JavaScript (vide → 0, illisible → NaN rendu 0).</summary>
    private static double JsNumber(string text) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : text.Trim().Length == 0 ? 0 : double.NaN;

    /// <summary>`fieldChangeLabels` : nom lisible de chaque identifiant du journal.</summary>
    internal static async Task<Dictionary<string, string>> JournalLabelsAsync(IAppDbContext db, IEnumerable<string?> values, CancellationToken cancellationToken)
    {
        var ids = values.OfType<string>().Where(v => v.Length > 0).Distinct().ToList();
        var labels = new Dictionary<string, string>();
        if (ids.Count == 0) return labels;
        foreach (var r in await db.Clients.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, c.Name }).ToListAsync(cancellationToken)) labels[r.Id] = r.Name;
        foreach (var r in await db.Cities.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, c.Name }).ToListAsync(cancellationToken)) labels[r.Id] = r.Name;
        foreach (var r in await db.Agencies.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, c.Name }).ToListAsync(cancellationToken)) labels[r.Id] = r.Name;
        foreach (var r in await db.Districts.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, c.Name }).ToListAsync(cancellationToken)) labels[r.Id] = r.Name;
        foreach (var r in await db.Couriers.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, c.FirstName, c.LastName }).ToListAsync(cancellationToken)) labels[r.Id] = $"{r.FirstName} {r.LastName}".Trim();
        return labels;
    }
}
