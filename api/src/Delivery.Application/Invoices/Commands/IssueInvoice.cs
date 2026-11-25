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

/// <summary>`invoiceIssueSchema` : l'émission fige les lignes (TVA du jour), passe les colis « facturés », prévient le vendeur.</summary>
public sealed record IssueInvoiceCommand : IRequest<InvoiceDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? DueAt { get; init => field = Zod.Trim(value); }
}

public sealed class IssueInvoiceValidator : ZodValidator<IssueInvoiceCommand>
{
    public IssueInvoiceValidator()
    {
        RuleFor(x => x.DueAt).Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("invalid-date").When(x => !string.IsNullOrEmpty(x.DueAt));
    }
}

internal sealed class IssueInvoiceHandler(IAppDbContext db, ICurrentUser caller, ILogger<IssueInvoiceHandler> logger) : IRequestHandler<IssueInvoiceCommand, InvoiceDetailDto>
{
    public async Task<InvoiceDetailDto> Handle(IssueInvoiceCommand input, CancellationToken cancellationToken)
    {
        var invoice = await InvoiceRows.Scoped(db.Invoices.AsNoTracking(), caller).Where(i => i.Code == input.Code)
                          .Select(i => new
                          {
                              i.Id, i.Code, i.Status, i.ClientId, i.AgencyId, i.BillingType, i.ProVatRate, i.Client.IsPro, ClientVatRate = i.Client.VatRate,
                              Lines = i.Lines.Select(l => new
                              {
                                  l.Id, l.ParcelId, l.CodAmount, l.DeliveryFee, l.FailureFee, l.ChequePaid, l.Parcel.Code,
                                  PaidOut = l.Parcel.CodPayouts.Where(c => c.PaidOutParcelId != null).Select(c => c.Payout.Code).FirstOrDefault(),
                              }).ToList(),
                              Adjustments = i.Adjustments.Select(a => new { a.Id, a.Amount }).ToList(),
                          }).FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound($"Facture introuvable : {input.Code}");
        if (invoice.Status != InvoiceStatus.DRAFT) throw BusinessException.Conflict("Cette facture est déjà émise.", new { reason = "invoice-already-issued" });
        if (invoice.Lines.Count == 0 && invoice.Adjustments.Count == 0) throw BusinessException.Validation("Une facture sans colis ni ligne ne s’émet pas.");
        if (invoice.BillingType == BillingType.NORMAL && invoice.Lines.FirstOrDefault(l => l.PaidOut is not null) is { } paid)
        {
            throw BusinessException.Conflict($"Le COD du colis {paid.Code} a déjà été reversé par le bon de versement {paid.PaidOut} : retirez-le de la facture avant de l'émettre.",
                new { reason = "parcel-cod-already-paid-out", code = paid.Code, payoutCode = paid.PaidOut });
        }
        await ReturnRows.AssertAgencyReachableAsync(db, caller, invoice.AgencyId, cancellationToken);
        var now = DateTime.UtcNow;
        var vatRate = await InvoiceDrafts.VatRateAsync(db, cancellationToken);
        int? proRate = invoice.IsPro ? Billing.ProVatRateFor(true, invoice.ClientVatRate, await InvoiceDrafts.ProVatDefaultAsync(db, cancellationToken)) : null;
        var lineRate = proRate is null ? vatRate : 0;
        var professional = invoice.BillingType == BillingType.PROFESSIONAL;
        var lines = invoice.Lines.Select(l =>
        {
            var fee = Money.ToCents(l.DeliveryFee) + Money.ToCents(l.FailureFee);
            var vat = Billing.VatOn(fee, lineRate);
            var collected = professional ? 0 : Money.ToCents(l.CodAmount);
            var cheque = professional ? 0 : Money.ToCents(l.ChequePaid);
            return new { l.Id, l.ParcelId, Vat = vat, NetDue = collected - cheque - fee - vat, Cod = Money.ToCents(l.CodAmount), Delivery = Money.ToCents(l.DeliveryFee), Failure = Money.ToCents(l.FailureFee) };
        }).ToList();
        var adjustments = invoice.Adjustments.Select(a => new { a.Id, Amount = Money.ToCents(a.Amount), Vat = InvoiceMath.SignedVat(Money.ToCents(a.Amount), lineRate) }).ToList();
        var sums = new InvoiceLineSums(lines.Sum(l => l.Cod), lines.Sum(l => l.Delivery), lines.Sum(l => l.Failure), lines.Sum(l => l.Vat), lines.Sum(l => l.NetDue));
        var totals = proRate is { } rate
            ? InvoiceMath.ProTotals(sums, adjustments.Select(a => a.Amount), rate)
            : InvoiceMath.Totals(sums, adjustments.Select(a => (a.Amount, a.Vat)));
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            foreach (var line in lines)
            {
                await db.InvoiceLines.Where(l => l.Id == line.Id).ExecuteUpdateAsync(s => s.SetProperty(l => l.InvoicedParcelId, line.ParcelId).SetProperty(l => l.VatAmount, Money.FromCents(line.Vat))
                    .SetProperty(l => l.NetDue, Money.FromCents(line.NetDue)), cancellationToken);
                await db.Parcels.Where(p => p.Id == line.ParcelId).ExecuteUpdateAsync(s => s.SetProperty(p => p.BillingState, BillingState.INVOICED)
                    .SetProperty(p => p.FinancialStatus, FinancialStatus.FACTURE), cancellationToken);
            }
            foreach (var adjustment in adjustments)
            {
                await db.InvoiceAdjustments.Where(a => a.Id == adjustment.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.VatAmount, Money.FromCents(adjustment.Vat)), cancellationToken);
            }
            var row = await db.Invoices.FirstAsync(i => i.Id == invoice.Id, cancellationToken);
            row.Status = InvoiceStatus.ISSUED;
            row.IssuedAt = now;
            row.IssuedById = caller.UserId;
            row.DueAt = string.IsNullOrEmpty(input.DueAt) ? null : StatementLists.Day(input.DueAt).AddDays(1).AddMilliseconds(-1);
            row.VatRate = proRate ?? vatRate;
            if (proRate is not null || invoice.ProVatRate is not null) row.ProVatRate = proRate;
            row.TotalCod = Money.FromCents(totals.Cod);
            row.TotalDeliveryFee = Money.FromCents(totals.DeliveryFee);
            row.TotalFailureFee = Money.FromCents(totals.FailureFee);
            row.TotalAdjustment = Money.FromCents(totals.Adjustment);
            row.TotalVat = Money.FromCents(totals.Vat);
            row.TotalDue = Money.FromCents(totals.Due);
            row.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await TransferOrders.LinkIssuedInvoiceAsync(db, invoice.Code, invoice.Lines.Select(l => l.ParcelId).ToList(), cancellationToken);
            if (totals.Due == 0) await InvoiceRows.SettleAsync(db, invoice.Id, now, caller.UserId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Npgsql.PostgresException error) when (error.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
        {
            throw BusinessException.Conflict("Un colis de cette facture vient d’être facturé ailleurs. Retirez-le et réémettez.", new { reason = "parcel-already-invoiced" });
        }
        try
        {
            await Notifier.NotifyMerchantAsync(db, invoice.ClientId, "invoice-issued", new Dictionary<string, object> { ["code"] = invoice.Code },
                new Dictionary<string, string> { ["invoiceCode"] = invoice.Code }, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Notification « invoice-issued » non envoyée : {Message}", error.Message);
        }
        db.ChangeTracker.Clear();
        return await InvoiceRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
