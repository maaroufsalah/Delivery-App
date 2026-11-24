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

namespace Delivery.Application.Finance;

/// <summary>`codPayoutPaySchema` : le versement ; chaque colis est marqué versé, une seule fois.</summary>
public sealed record PayCodPayoutCommand : IRequest<CodPayoutDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? Method { get; init; }
    public string? BankAccountId { get; init => field = Zod.Trim(value); }
    public string? Reference { get; init => field = Zod.Trim(value); }
    public string? PaidAt { get; init => field = Zod.Trim(value); }
    public ProofFileDto? Proof { get; init; }
}

public sealed class PayCodPayoutValidator : ZodValidator<PayCodPayoutCommand>
{
    public PayCodPayoutValidator()
    {
        RuleFor(x => x.Method).Required().OneOf(["cash", "transfer", "check"]);
        RuleFor(x => x.Reference).MaxLength(120);
        RuleFor(x => x.PaidAt).Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("invalid-date").When(x => !string.IsNullOrEmpty(x.PaidAt));
        ProofRules.Proof(this, x => x.Proof);
    }
}

internal sealed class PayCodPayoutHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage) : IRequestHandler<PayCodPayoutCommand, CodPayoutDetailDto>
{
    public async Task<CodPayoutDetailDto> Handle(PayCodPayoutCommand input, CancellationToken cancellationToken)
    {
        var payout = await CodPayouts.Scoped(db.CodPayouts.AsNoTracking(), caller).Where(p => p.Code == input.Code)
                         .Select(p => new
                         {
                             p.Id, p.Status, p.AgencyId,
                             Lines = p.Parcels.Select(l => new
                             {
                                 l.ParcelId, l.Amount, l.Parcel.Code,
                                 NormalInvoice = l.Parcel.InvoiceLines.Where(i => i.InvoicedParcelId != null && i.Invoice.BillingType == BillingType.NORMAL).Select(i => i.Invoice.Code).FirstOrDefault(),
                             }).ToList(),
                         }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound($"Bon de versement introuvable : {input.Code}");
        if (payout.Status == CodPayoutStatus.PAID) throw BusinessException.Conflict("Ce bon de versement est déjà versé.", new { reason = "payout-already-paid" });
        if (payout.Lines.Count == 0) throw BusinessException.Validation("Un bon de versement sans colis ne se verse pas.");
        foreach (var line in payout.Lines) if (line.NormalInvoice is { } invoice) throw CodPayouts.AlreadyInvoiced(line.Code, invoice);
        await ReturnRows.AssertAgencyReachableAsync(db, caller, payout.AgencyId, cancellationToken);
        if (!string.IsNullOrEmpty(input.BankAccountId) && !await db.BankAccounts.AnyAsync(a => a.Id == input.BankAccountId && a.DeletedAt == null, cancellationToken))
        {
            throw BusinessException.NotFound("Compte bancaire introuvable.");
        }
        var now = DateTime.UtcNow;
        var paidAt = string.IsNullOrEmpty(input.PaidAt) ? now : DateTime.SpecifyKind(PettyCashDays.Parse(input.PaidAt).ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Utc);
        var total = payout.Lines.Sum(l => Money.ToCents(l.Amount));
        var proof = input.Proof is null ? null : await storage.PutAsync($"cod-payouts/{payout.Id}", Convert.FromBase64String(input.Proof.Content!), input.Proof.MimeType!, cancellationToken);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            foreach (var line in payout.Lines)
            {
                await db.CodPayoutParcels.Where(l => l.PayoutId == payout.Id && l.ParcelId == line.ParcelId).ExecuteUpdateAsync(s => s.SetProperty(l => l.PaidOutParcelId, line.ParcelId), cancellationToken);
            }
            var row = await db.CodPayouts.FirstAsync(p => p.Id == payout.Id, cancellationToken);
            row.Status = CodPayoutStatus.PAID;
            row.PaidAt = paidAt;
            row.PaidById = caller.UserId;
            row.Amount = Money.FromCents(total);
            row.Method = Enum.Parse<PaymentMethod>(input.Method!.ToUpperInvariant());
            row.BankAccountId = ReturnRows.Text(input.BankAccountId);
            row.Reference = ReturnRows.Text(input.Reference);
            row.UpdatedAt = now;
            if (proof is not null) { row.ProofName = input.Proof!.Name; row.ProofMimeType = input.Proof.MimeType; row.ProofPath = proof.Key; }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception error) when (error is DbUpdateException or Npgsql.PostgresException)
        {
            db.ChangeTracker.Clear();
            if (proof is not null) await storage.RemoveAsync(proof.Key, CancellationToken.None);
            var unique = error is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation } || error.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation };
            if (unique) throw BusinessException.Conflict("Le COD d’un colis de ce bon vient d’être versé ailleurs. Retirez-le et recommencez.", new { reason = "parcel-already-paid-out" });
            throw;
        }
        return await CodPayouts.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
