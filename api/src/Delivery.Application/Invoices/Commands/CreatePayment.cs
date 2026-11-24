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

/// <summary>`paymentSchema` : un règlement, rattaché ou non à une facture émise ; la facture se solde quand le compte y est.</summary>
public sealed record CreatePaymentCommand : IRequest<PaymentListItemDto>
{
    public string? InvoiceId { get; init => field = Zod.Trim(value); }
    public string? ClientId { get; init; }
    public string? AgencyId { get; init; }
    public double? Amount { get; init; }
    public string? Method { get; init; }
    public string? BankAccountId { get; init => field = Zod.Trim(value); }
    public string? Reference { get; init => field = Zod.Trim(value); }
    public string? Note { get; init => field = Zod.Trim(value); }
    public string? PaidAt { get; init => field = Zod.Trim(value); }
    public string? ChequeNumber { get; init => field = Zod.Trim(value); }
    public string? ChequeBank { get; init => field = Zod.Trim(value); }
    public string? ChequeDate { get; init => field = Zod.Trim(value); }
    public ProofFileDto? Proof { get; init; }
}

public sealed class CreatePaymentValidator : ZodValidator<CreatePaymentCommand>
{
    public CreatePaymentValidator()
    {
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.Amount).Must(v => v is not null).WithMessage("required").Must(v => v is null || v.Value > 0).WithMessage("must-be-positive")
            .Must(v => v is null || v.Value <= 1_000_000).WithMessage("amount-too-large");
        RuleFor(x => x.Method).OneOf(["cash", "transfer", "check"]);
        RuleFor(x => x.Reference).MaxLength(120);
        RuleFor(x => x.Note).MaxLength(500);
        RuleFor(x => x.PaidAt).Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("invalid-date").When(x => !string.IsNullOrEmpty(x.PaidAt));
        RuleFor(x => x.ChequeNumber).MaxLength(40);
        RuleFor(x => x.ChequeBank).MaxLength(80);
        RuleFor(x => x.ChequeDate).Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("invalid-date").When(x => !string.IsNullOrEmpty(x.ChequeDate));
        ProofRules.Proof(this, x => x.Proof);
    }

    protected override void Refine(CreatePaymentCommand value, Action<string, string> issue)
    {
        var method = value.Method ?? "cash";
        if (method == "check" && string.IsNullOrEmpty(value.ChequeNumber)) issue(ValidationPaths.Raw + "chequeNumber", "cheque-number-required");
        if (method != "cash" && string.IsNullOrEmpty(value.BankAccountId)) issue(ValidationPaths.Raw + "bankAccountId", "bank-account-required");
    }
}

internal sealed class CreatePaymentHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage) : IRequestHandler<CreatePaymentCommand, PaymentListItemDto>
{
    public async Task<PaymentListItemDto> Handle(CreatePaymentCommand input, CancellationToken cancellationToken)
    {
        await ReturnRows.AssertAgencyReachableAsync(db, caller, input.AgencyId!, cancellationToken);
        if (!await db.Clients.AnyAsync(c => c.Id == input.ClientId && c.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("E-commerçant introuvable.");
        if (!string.IsNullOrEmpty(input.BankAccountId) && !await db.BankAccounts.AnyAsync(a => a.Id == input.BankAccountId && a.DeletedAt == null, cancellationToken))
        {
            throw BusinessException.NotFound("Compte bancaire introuvable.");
        }
        var invoiceId = ReturnRows.Text(input.InvoiceId);
        var invoice = invoiceId is null ? null
            : await InvoiceRows.Scoped(db.Invoices.AsNoTracking(), caller).Where(i => i.Id == invoiceId).Select(i => new { i.Id, i.Status, i.ClientId, i.TotalDue }).FirstOrDefaultAsync(cancellationToken)
              ?? throw BusinessException.NotFound("Facture introuvable.");
        if (invoice is not null)
        {
            if (invoice.Status == InvoiceStatus.DRAFT) throw BusinessException.Conflict("Cette facture n’est pas encore émise.", new { reason = "invoice-draft" });
            if (invoice.Status == InvoiceStatus.PAID) throw BusinessException.Conflict("Cette facture est déjà soldée.", new { reason = "invoice-already-paid" });
            if (invoice.ClientId != input.ClientId) throw BusinessException.Conflict("Cette facture appartient à un autre e-commerçant.", new { reason = "invoice-other-client" });
        }
        var now = DateTime.UtcNow;
        var paidAt = string.IsNullOrEmpty(input.PaidAt) ? now : DateTime.SpecifyKind(PettyCashDays.Parse(input.PaidAt).ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Utc);
        var amount = (long)Math.Round(input.Amount!.Value * 100, MidpointRounding.AwayFromZero);
        var proof = input.Proof is null ? null : await storage.PutAsync($"cheques/{caller.CompanyId}", Convert.FromBase64String(input.Proof.Content!), input.Proof.MimeType!, cancellationToken);
        var payment = new Payment
        {
            InvoiceId = invoice?.Id, ClientId = input.ClientId!, AgencyId = input.AgencyId!, Amount = Money.FromCents(amount), Method = Enum.Parse<PaymentMethod>((input.Method ?? "cash").ToUpperInvariant()),
            BankAccountId = ReturnRows.Text(input.BankAccountId), Reference = ReturnRows.Text(input.Reference), Note = ReturnRows.Text(input.Note), PaidAt = paidAt,
            ChequeNumber = ReturnRows.Text(input.ChequeNumber), ChequeBank = ReturnRows.Text(input.ChequeBank),
            ChequeDate = string.IsNullOrEmpty(input.ChequeDate) ? null : PettyCashDays.Parse(input.ChequeDate), ProofName = proof is null ? null : input.Proof!.Name,
            ProofMimeType = proof is null ? null : input.Proof!.MimeType, ProofPath = proof?.Key, CreatedById = caller.UserId, CreatedAt = now, UpdatedAt = now,
        };
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            if (invoice is not null)
            {
                await db.Database.ExecuteSqlAsync($"SELECT id FROM invoices WHERE id = {invoice.Id} FOR UPDATE", cancellationToken);
                if (await db.Invoices.AnyAsync(i => i.Id == invoice.Id && i.Status == InvoiceStatus.PAID, cancellationToken))
                {
                    throw BusinessException.Conflict("Cette facture vient d’être soldée.", new { reason = "invoice-already-paid" });
                }
            }
            db.Payments.Add(payment);
            await db.SaveChangesAsync(cancellationToken);
            if (invoice is not null)
            {
                var paid = (await db.Payments.Where(p => p.InvoiceId == invoice.Id).Select(p => p.Amount).ToListAsync(cancellationToken)).Sum(a => Money.ToCents(a));
                if (paid >= Math.Abs(Money.ToCents(invoice.TotalDue))) await InvoiceRows.SettleAsync(db, invoice.Id, paidAt, caller.UserId, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        return (await InvoiceRows.PaymentsAsync(db, db.Payments.AsNoTracking().Where(p => p.Id == payment.Id), cancellationToken)).First();
    }
}
