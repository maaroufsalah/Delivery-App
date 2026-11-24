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

/// <summary>`invoiceMonthlySchema` : la facture du mois d'un vendeur professionnel, colis facturables de la période.</summary>
public sealed record CreateMonthlyInvoiceCommand : IRequest<InvoiceDetailDto>
{
    [JsonIgnore]
    public string? Kind { get; init; }

    public string? ClientId { get; init; }
    public string? AgencyId { get; init; }
    public string? PeriodStart { get; init => field = Zod.Trim(value); }
    public string? PeriodEnd { get; init => field = Zod.Trim(value); }
    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class CreateMonthlyInvoiceValidator : ZodValidator<CreateMonthlyInvoiceCommand>
{
    public CreateMonthlyInvoiceValidator()
    {
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.PeriodStart).Required().Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("invalid-date");
        RuleFor(x => x.PeriodEnd).Required().Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("invalid-date");
        RuleFor(x => x.Note).MaxLength(500);
    }

    protected override void Refine(CreateMonthlyInvoiceCommand value, Action<string, string> issue)
    {
        if (value.PeriodStart is { } a && value.PeriodEnd is { } b && string.CompareOrdinal(a, b) > 0) issue(ValidationPaths.Raw + "periodEnd", "period-reversed");
    }
}

internal sealed class CreateMonthlyInvoiceHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<CreateMonthlyInvoiceCommand, InvoiceDetailDto>
{
    public async Task<InvoiceDetailDto> Handle(CreateMonthlyInvoiceCommand input, CancellationToken cancellationToken)
    {
        await ReturnRows.AssertAgencyReachableAsync(db, caller, input.AgencyId!, cancellationToken);
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == input.ClientId && c.DeletedAt == null).Select(c => new { c.Id, c.Name, c.BillingType, c.IsPro }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound("E-commerçant introuvable.");
        InvoiceRows.AssertKind(client.Name, client.IsPro, input.Kind);
        if (client.BillingType != BillingType.PROFESSIONAL)
        {
            throw BusinessException.Conflict($"{client.Name} n’est pas un e-commerçant professionnel : sa facture se compose colis par colis.", new { reason = "client-not-professional", clientId = client.Id });
        }
        var now = clock.UtcNow;
        var start = StatementLists.Day(input.PeriodStart!);
        var end = StatementLists.Day(input.PeriodEnd!).AddDays(1).AddMilliseconds(-1);
        var where = InvoiceRows.BillableNow(ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller)
            .Where(p => p.ClientId == client.Id && p.BillingState == BillingState.TO_INVOICE && !p.InvoiceLines.Any() && p.StatusAt >= start && p.StatusAt <= end), now);
        var parcels = await InvoiceRows.MoneyOf(where.OrderBy(p => p.StatusAt)).ToListAsync(cancellationToken);
        if (parcels.Count == 0) throw BusinessException.Conflict($"Aucun colis facturable pour {client.Name} sur cette période.", new { reason = "no-billable-parcel", clientId = client.Id });
        var (prefix, yearly) = await InvoiceDrafts.NumberingAsync(db, cancellationToken);
        var vatRate = await InvoiceDrafts.VatRateAsync(db, cancellationToken);
        string code;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var draft = await InvoiceDrafts.OpenDraftOfAsync(db, client.Id, cancellationToken);
            if (draft is not null) await ReturnRows.AssertAgencyReachableAsync(db, caller, draft.AgencyId, cancellationToken);
            Invoice invoice;
            if (draft is not null)
            {
                draft.PeriodStart = draft.PeriodStart is { } ds && ds < start ? ds : start;
                draft.PeriodEnd = draft.PeriodEnd is { } de && de > end ? de : end;
                invoice = draft;
            }
            else
            {
                invoice = new Invoice
                {
                    Code = await DocumentCodes.ReserveAsync(db, prefix, now, cancellationToken, yearly), ClientId = client.Id, AgencyId = input.AgencyId!, BillingType = client.BillingType,
                    PeriodStart = start, PeriodEnd = end, Note = ReturnRows.Text(input.Note), CreatedById = caller.UserId, CreatedAt = now, UpdatedAt = now,
                };
                db.Invoices.Add(invoice);
            }
            await db.SaveChangesAsync(cancellationToken);
            var billingType = draft?.BillingType ?? client.BillingType;
            var existing = (await db.InvoiceLines.Where(l => l.InvoiceId == invoice.Id).Select(l => l.ParcelId).ToListAsync(cancellationToken)).ToHashSet();
            foreach (var parcel in parcels.Where(p => !existing.Contains(p.Id)))
            {
                db.InvoiceLines.Add(InvoiceRows.LineOf(invoice.Id, parcel.Id, parcel.Code, parcel.StatusId, InvoiceRows.LineAmounts(parcel.Billable, client.IsPro ? 0 : vatRate, billingType, parcel.ChequePaid)));
            }
            await db.SaveChangesAsync(cancellationToken);
            await InvoiceDrafts.RefreshTotalsAsync(db, invoice.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            code = invoice.Code;
        }
        db.ChangeTracker.Clear();
        return await InvoiceRows.DetailAsync(db, caller, code, cancellationToken);
    }
}
