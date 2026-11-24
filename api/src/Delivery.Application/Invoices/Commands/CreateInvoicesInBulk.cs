using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Finance;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.PettyCash;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Invoices;

/// <summary>
/// `bulkInvoiceSchema` : une facture par vendeur coché, avec tous ses colis remis par le livreur et jamais facturés.
/// Correctif : comme `pendingByClient` et l'ajout manuel, un colis dont le COD est déjà versé par bon de versement
/// n'entre pas sur une facture de régime normal (Speedy l'y ajoutait, et le COD était dû deux fois).
/// </summary>
public sealed record CreateInvoicesInBulkCommand : IRequest<BulkInvoiceResultDto>
{
    [JsonIgnore]
    public string? Kind { get; init; }

    public IReadOnlyList<string>? ClientIds { get; init; }
    public string? AgencyId { get; init => field = Zod.Trim(value); }
    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class CreateInvoicesInBulkValidator : ZodValidator<CreateInvoicesInBulkCommand>
{
    public CreateInvoicesInBulkValidator()
    {
        RuleFor(x => x.ClientIds).Required("array").Custom((ids, context) =>
        {
            if (ids is null) return;
            if (ids.Count < 1) context.AddFailure(ValidationPaths.Raw + "clientIds", "required");
            if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + "clientIds", "too-many");
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"clientIds.{i}", Zod.TooSmallString(1));
        });
        RuleFor(x => x.Note).MaxLength(500);
    }
}

internal sealed class CreateInvoicesInBulkHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateInvoicesInBulkCommand, BulkInvoiceResultDto>
{
    public async Task<BulkInvoiceResultDto> Handle(CreateInvoicesInBulkCommand input, CancellationToken cancellationToken)
    {
        var ids = input.ClientIds!;
        var clients = await db.Clients.AsNoTracking().Where(c => ids.Contains(c.Id) && c.DeletedAt == null).Select(c => new { c.Id, c.Name, c.BillingType, c.AgencyId, c.IsPro })
            .ToDictionaryAsync(c => c.Id, cancellationToken);
        foreach (var client in clients.Values) InvoiceRows.AssertKind(client.Name, client.IsPro, input.Kind);
        var created = new List<BulkInvoiceCreatedDto>();
        var skipped = new List<BulkInvoiceSkippedDto>();
        var (prefix, yearly) = await InvoiceDrafts.NumberingAsync(db, cancellationToken);
        var vatRate = await InvoiceDrafts.VatRateAsync(db, cancellationToken);
        foreach (var clientId in ids)
        {
            if (!clients.TryGetValue(clientId, out var client)) { skipped.Add(new BulkInvoiceSkippedDto(clientId, clientId, "client-unknown")); continue; }
            var rows = await ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => p.ClientId == client.Id && p.FinancialStatus == FinancialStatus.REMIS_LIVREUR && !p.InvoiceLines.Any())
                .OrderBy(p => p.StatusAt).Select(p => new { PaidOut = p.CodPayouts.Any(l => l.PaidOutParcelId != null), p.Id }).ToListAsync(cancellationToken);
            if (rows.Count == 0) { skipped.Add(new BulkInvoiceSkippedDto(clientId, client.Name, "no-billable-parcel")); continue; }
            var now = DateTime.UtcNow;
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var draft = await InvoiceDrafts.OpenDraftOfAsync(db, client.Id, cancellationToken);
            var parcelIds = rows.Select(r => r.Id).ToList();
            var parcels = await InvoiceRows.MoneyOf(db.Parcels.AsNoTracking().Where(p => parcelIds.Contains(p.Id)).OrderBy(p => p.StatusAt)).ToListAsync(cancellationToken);
            var agencyId = draft?.AgencyId ?? (string.IsNullOrEmpty(input.AgencyId) ? null : input.AgencyId) ?? client.AgencyId ?? parcels.FirstOrDefault()?.AgencyId;
            if (agencyId is null) { skipped.Add(new BulkInvoiceSkippedDto(clientId, client.Name, "no-agency")); continue; }
            try
            {
                await ReturnRows.AssertAgencyReachableAsync(db, caller, agencyId, cancellationToken);
            }
            catch (BusinessException)
            {
                skipped.Add(new BulkInvoiceSkippedDto(clientId, client.Name, "agency-out-of-scope"));
                continue;
            }
            var billingType = draft?.BillingType ?? client.BillingType;
            var paidOut = rows.Where(r => r.PaidOut).Select(r => r.Id).ToHashSet();
            var invoice = draft;
            if (invoice is null)
            {
                invoice = new Invoice
                {
                    Code = await DocumentCodes.ReserveAsync(db, prefix, now, cancellationToken, yearly), ClientId = client.Id, AgencyId = agencyId, BillingType = billingType,
                    Note = ReturnRows.Text(input.Note), CreatedById = caller.UserId, CreatedAt = now, UpdatedAt = now,
                };
                db.Invoices.Add(invoice);
                await db.SaveChangesAsync(cancellationToken);
            }
            var existing = (await db.InvoiceLines.Where(l => l.InvoiceId == invoice.Id).Select(l => l.ParcelId).ToListAsync(cancellationToken)).ToHashSet();
            foreach (var parcel in parcels.Where(p => !existing.Contains(p.Id) && !(billingType == BillingType.NORMAL && paidOut.Contains(p.Id))))
            {
                db.InvoiceLines.Add(InvoiceRows.LineOf(invoice.Id, parcel.Id, parcel.Code, parcel.StatusId, InvoiceRows.LineAmounts(parcel.Billable, client.IsPro ? 0 : vatRate, billingType, parcel.ChequePaid)));
            }
            await db.SaveChangesAsync(cancellationToken);
            await InvoiceDrafts.RefreshTotalsAsync(db, invoice.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            db.ChangeTracker.Clear();
            created.Add(new BulkInvoiceCreatedDto(clientId, client.Name, invoice.Code, parcels.Count, draft is not null));
        }
        return new BulkInvoiceResultDto(created, skipped);
    }
}
