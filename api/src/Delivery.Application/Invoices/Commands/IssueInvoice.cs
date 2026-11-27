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
                              i.Id, i.Code, i.Status, i.ClientId, i.AgencyId, i.BillingType, i.ProVatRate, i.Client.IsPro, i.Client.IsInternal, ClientVatRate = i.Client.VatRate,
                              Lines = i.Lines.Select(l => new
                              {
                                  l.Id, l.ParcelId, l.CodAmount, l.DeliveryFee, l.FailureFee, l.ChequePaid, l.Parcel.Code,
                                  PaidOut = l.Parcel.CodPayouts.Where(c => c.PaidOutParcelId != null).Select(c => c.Payout.Code).FirstOrDefault(),
                              }).ToList(),
                              Adjustments = i.Adjustments.Select(a => new { a.Id, a.Amount, a.Kind }).ToList(),
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
        // Les lignes hors colis, au même taux que les colis ; un report n'a pas de TVA (le net reporté est déjà TTC).
        var adjustments = invoice.Adjustments.Select(a =>
        {
            var amount = Money.ToCents(a.Amount);
            var exempt = a.Kind == InvoiceAdjustmentKind.CARRY_OVER;
            return new { a.Id, Amount = amount, Vat = exempt ? 0 : InvoiceMath.SignedVat(amount, lineRate), Exempt = exempt };
        }).ToList();
        // Les reports en attente du vendeur : chaque facture « Reportée » que rien n'a absorbée devient une ligne
        // « Report FA-… : −X MAD », due par lui. Absorbée quel que soit le type du client ce jour-là : un report posé ne se perd pas.
        var carryOvers = (await db.Invoices.AsNoTracking()
                .Where(i => i.ClientId == invoice.ClientId && i.Status == InvoiceStatus.CARRIED_OVER && i.CarriedIntoId == null && i.Id != invoice.Id)
                .OrderBy(i => i.IssuedAt).ThenBy(i => i.Code).Select(i => new { i.Id, i.Code, i.TotalDue }).ToListAsync(cancellationToken))
            .Select(i => new { FromId = i.Id, i.Code, Amount = CarryOver.LineAmount(Money.ToCents(i.TotalDue)) }).ToList();
        var sums = new InvoiceLineSums(lines.Sum(l => l.Cod), lines.Sum(l => l.Delivery), lines.Sum(l => l.Failure), lines.Sum(l => l.Vat), lines.Sum(l => l.NetDue));
        var totals = proRate is { } rate
            ? InvoiceMath.ProTotals(sums, adjustments.Select(a => (a.Amount, a.Exempt)).Concat(carryOvers.Select(c => (c.Amount, true))), rate)
            : InvoiceMath.Totals(sums, adjustments.Select(a => (a.Amount, a.Vat)).Concat(carryOvers.Select(c => (c.Amount, 0L))));
        // Net < 0 d'un vendeur ordinaire : « Reportée », jamais « Émise » à régler. Pro, interne et régime professionnel gardent l'émission.
        var carriedOver = CarryOver.CarriesOver(totals.Due, invoice.IsPro, invoice.IsInternal, invoice.BillingType);
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
            if (carryOvers.Count > 0)
            {
                // Sous transaction : un report absorbé par une émission concurrente ne l'est pas deux fois.
                var ids = carryOvers.Select(c => c.FromId).ToList();
                var claimed = await db.Invoices.Where(i => ids.Contains(i.Id) && i.Status == InvoiceStatus.CARRIED_OVER && i.CarriedIntoId == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.CarriedIntoId, invoice.Id), cancellationToken);
                if (claimed != carryOvers.Count)
                {
                    throw BusinessException.Conflict("Un report de ce vendeur vient d’être absorbé par une autre facture : réessayez l’émission.", new { reason = "carry-over-already-claimed" });
                }
                foreach (var carry in carryOvers)
                {
                    db.InvoiceAdjustments.Add(new InvoiceAdjustment
                    {
                        InvoiceId = invoice.Id, Kind = InvoiceAdjustmentKind.CARRY_OVER, Amount = Money.FromCents(carry.Amount), VatAmount = 0m, Reason = CarryOver.Reason(carry.Code, carry.Amount),
                        Reference = carry.Code, CarriedFromInvoiceId = carry.FromId, CreatedById = caller.UserId, CreatedByName = CarryOver.AuthorName, CreatedAt = now,
                    });
                }
            }
            var row = await db.Invoices.FirstAsync(i => i.Id == invoice.Id, cancellationToken);
            row.Status = carriedOver ? InvoiceStatus.CARRIED_OVER : InvoiceStatus.ISSUED;
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
            else if (carriedOver)
            {
                // Reportée : ses colis sont réglés pour elle (le net part sur la suivante), comme à zéro.
                var parcelIds = lines.Select(l => l.ParcelId).ToList();
                await db.Parcels.Where(p => parcelIds.Contains(p.Id)).ExecuteUpdateAsync(s => s.SetProperty(p => p.BillingState, BillingState.REIMBURSED)
                    .SetProperty(p => p.FinancialStatus, FinancialStatus.REMBOURSE), cancellationToken);
            }
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
