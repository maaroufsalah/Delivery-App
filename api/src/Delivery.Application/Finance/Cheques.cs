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
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>`ChequesService` : portée, parcours et écritures d'un chèque.</summary>
public static class Cheques
{
    public static readonly string[] Statuses = ["recu-livreur", "remis-admin", "recu-admin", "transmis-vendeur", "recu-vendeur", "rejete", "annule"];
    private static readonly string[] Flow = ["recu-livreur", "remis-admin", "recu-admin", "transmis-vendeur", "recu-vendeur"];
    public const string PendingFilter = "en-cours";

    public static string? Next(string status) => Array.IndexOf(Flow, status) is var at and >= 0 && at + 1 < Flow.Length ? Flow[at + 1] : null;

    public static bool CanReject(string status) => status is "recu-admin" or "transmis-vendeur" or "recu-vendeur";

    public static bool CanCancel(string status) => status is not ("recu-vendeur" or "annule" or "rejete");

    /// <summary>`scoped` : administrateur tout, livreur les siens, vendeur ceux de ses colis, agent ceux de son agence.</summary>
    public static IQueryable<Cheque> Scoped(IQueryable<Cheque> cheques, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return cheques;
        if (caller.CourierId is { } courierId) return cheques.Where(c => c.CourierId == courierId);
        if (caller.ClientId is { } clientId) return cheques.Where(c => c.Parcel.ClientId == clientId);
        if (caller.AgencyId is { } agencyId) return cheques.Where(c => c.Parcel.AgencyId == agencyId);
        return cheques.Where(c => c.Id == Scope.DenyAllId);
    }

    /// <summary>`scoped` du bon de remise : sans branche vendeur (le vendeur est refusé avant).</summary>
    public static IQueryable<Cheque> SlipScoped(IQueryable<Cheque> cheques, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return cheques;
        if (caller.CourierId is { } courierId) return cheques.Where(c => c.CourierId == courierId);
        if (caller.AgencyId is { } agencyId) return cheques.Where(c => c.Parcel.AgencyId == agencyId);
        return cheques.Where(c => c.Id == Scope.DenyAllId);
    }

    private sealed record Row(string Id, ChequeStatus Status, string Number, decimal Amount, string? Bank, DateOnly? ChequeDate, DateTime ReceivedAt, string PayerName, string ParcelId,
        string ParcelCode, string ClientName, string CourierId, string CourierName, string? InvoiceCode, InvoiceStatus? InvoiceStatus);

    private static IQueryable<Row> Rows(IQueryable<Cheque> cheques) => cheques.Select(c => new Row(c.Id, c.Status, c.Number, c.Amount, c.Bank, c.ChequeDate, c.ReceivedAt, c.PayerName,
        c.ParcelId, c.Parcel.Code, c.Parcel.Client.Name, c.CourierId, c.Courier.FirstName + " " + c.Courier.LastName, c.Invoice != null ? c.Invoice.Code : null,
        c.Invoice != null ? c.Invoice.Status : null));

    private static T Fill<T>(T item, Row row, ICurrentUser caller) where T : ChequeListItemDto
    {
        var merchant = caller.ClientId is not null;
        item.Id = row.Id; item.Status = Money.ToWire(row.Status); item.Number = row.Number; item.Amount = Money.ToCents(row.Amount); item.Bank = row.Bank;
        item.ChequeDate = row.ChequeDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); item.ReceivedAt = row.ReceivedAt; item.PayerName = row.PayerName; item.ParcelId = row.ParcelId;
        item.ParcelCode = row.ParcelCode; item.ClientName = row.ClientName; item.CourierId = merchant ? string.Empty : row.CourierId; item.CourierName = row.CourierName;
        item.InvoiceCode = merchant && row.InvoiceStatus == InvoiceStatus.DRAFT ? null : row.InvoiceCode;
        return item;
    }

    public static async Task<List<ChequeListItemDto>> ListAsync(IQueryable<Cheque> cheques, ICurrentUser caller, CancellationToken cancellationToken) =>
        (await Rows(cheques).ToListAsync(cancellationToken)).Select(r => Fill(new ChequeListItemDto(), r, caller)).ToList();

    public static async Task<ChequeDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, string id, CancellationToken cancellationToken)
    {
        var scoped = Scoped(db.Cheques.AsNoTracking(), caller).Where(c => c.Id == id);
        var row = await Rows(scoped).FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Chèque introuvable : {id}");
        var extra = await scoped.Select(c => new { c.Note, c.RejectionReason }).FirstAsync(cancellationToken);
        var events = await db.ChequeEvents.AsNoTracking().Where(e => e.ChequeId == id).OrderBy(e => e.CreatedAt)
            .Select(e => new { e.Id, e.Status, e.Kind, e.Comment, e.AuthorName, e.CreatedAt }).ToListAsync(cancellationToken);
        var merchant = caller.ClientId is not null;
        var detail = Fill(new ChequeDetailDto(), row, caller);
        var status = detail.Status;
        detail.Note = merchant ? null : extra.Note;
        detail.RejectionReason = extra.RejectionReason;
        detail.Events = events.Select(e => new ChequeEventDto(e.Id, Money.ToWire(e.Status), e.Kind == "slip" ? "slip" : "transition", merchant ? null : e.Comment,
            merchant ? string.Empty : e.AuthorName ?? string.Empty, e.CreatedAt)).ToList();
        detail.NextStatus = merchant || (caller.CourierId is not null && status != "recu-livreur") ? null : Next(status);
        detail.Rejectable = !merchant && caller.CourierId is null && CanReject(status);
        detail.Cancellable = !merchant && caller.CourierId is null && CanCancel(status);
        return detail;
    }

    public static async Task<(string Id, string Status, string Number, string ParcelId)> LoadAsync(IAppDbContext db, ICurrentUser caller, string id, CancellationToken cancellationToken)
    {
        var cheque = await Scoped(db.Cheques.AsNoTracking(), caller).Where(c => c.Id == id).Select(c => new { c.Id, c.Status, c.Number, c.ParcelId }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound($"Chèque introuvable : {id}");
        return (cheque.Id, Money.ToWire(cheque.Status), cheque.Number, cheque.ParcelId);
    }

    /// <summary>`write` : le nouveau statut, ses colonnes de date, et l'événement, en une transaction.</summary>
    public static async Task WriteAsync(IAppDbContext db, ICurrentUser caller, string chequeId, string status, string? comment, DateTime now, string? rejectionReason,
        CancellationToken cancellationToken)
    {
        var authorName = await Remittances.AuthorNameAsync(db, caller.UserId, cancellationToken);
        var value = Enum.Parse<ChequeStatus>(Money.FromWire(status));
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var cheque = await db.Cheques.FirstAsync(c => c.Id == chequeId, cancellationToken);
        cheque.Status = value;
        cheque.UpdatedAt = now;
        switch (status)
        {
            case "remis-admin": cheque.HandedOverAt = now; break;
            case "recu-admin": cheque.ReceivedByAdminAt = now; break;
            case "transmis-vendeur": cheque.TransmittedAt = now; break;
            case "recu-vendeur": cheque.MerchantReceivedAt = now; break;
            case "rejete": cheque.RejectedAt = now; cheque.RejectionReason = rejectionReason; break;
            case "annule": cheque.CancelledAt = now; break;
        }
        db.ChequeEvents.Add(new ChequeEvent { ChequeId = chequeId, Status = value, Comment = string.IsNullOrEmpty(comment) ? null : comment, AuthorId = caller.UserId, AuthorName = authorName, CreatedAt = now });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>`returnToDue` : un chèque rejeté ou annulé redevient dû au vendeur (reversements et factures brouillons recalculés).</summary>
    public static async Task ReturnToDueAsync(IAppDbContext db, ICurrentUser caller, string parcelId, string reason, CancellationToken cancellationToken)
    {
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await CodPayoutDrafts.RefreshDraftsForAsync(db, parcelId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        await InvoiceDraftReplay.ChequeLinesAsync(db, caller, parcelId, await Remittances.AuthorNameAsync(db, caller.UserId, cancellationToken), reason, cancellationToken);
    }
}
