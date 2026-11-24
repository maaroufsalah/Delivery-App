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

namespace Delivery.Application.Finance;

/// <summary>`statementPaySchema` : le règlement du relevé validé (espèces, virement ou chèque).</summary>
public sealed record PayCourierStatementCommand : IRequest<CourierStatementDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? Method { get; init; }
    public string? BankAccountId { get; init => field = Zod.Trim(value); }
    public string? Reference { get; init => field = Zod.Trim(value); }
    public string? ChequeNumber { get; init => field = Zod.Trim(value); }
    public string? ChequeBank { get; init => field = Zod.Trim(value); }
    public string? ChequeDate { get; init => field = Zod.Trim(value); }
    public ProofFileDto? Proof { get; init; }
}

public sealed class PayCourierStatementValidator : ZodValidator<PayCourierStatementCommand>
{
    public PayCourierStatementValidator()
    {
        RuleFor(x => x.Method).OneOf(["cash", "transfer", "check"]);
        RuleFor(x => x.Reference).MaxLength(120);
        RuleFor(x => x.ChequeNumber).MaxLength(40);
        RuleFor(x => x.ChequeBank).MaxLength(80);
        RuleFor(x => x.ChequeDate).Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("invalid-date").When(x => !string.IsNullOrEmpty(x.ChequeDate));
        ProofRules.Proof(this, x => x.Proof);
    }

    protected override void Refine(PayCourierStatementCommand value, Action<string, string> issue)
    {
        var method = value.Method ?? "cash";
        if (method == "check" && string.IsNullOrEmpty(value.ChequeNumber)) issue(ValidationPaths.Raw + "chequeNumber", "cheque-number-required");
        if (method != "cash" && string.IsNullOrEmpty(value.BankAccountId)) issue(ValidationPaths.Raw + "bankAccountId", "bank-account-required");
    }
}

internal sealed class PayCourierStatementHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage) : IRequestHandler<PayCourierStatementCommand, CourierStatementDetailDto>
{
    public async Task<CourierStatementDetailDto> Handle(PayCourierStatementCommand input, CancellationToken cancellationToken)
    {
        var statement = await CourierStatements.Scoped(db.CourierStatements.AsNoTracking(), caller).Where(s => s.Code == input.Code).Select(s => new { s.Id, s.Status, s.AgencyId })
                            .FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Relevé introuvable : {input.Code}");
        if (statement.Status == CourierStatementStatus.DRAFT) throw BusinessException.Conflict("Ce relevé n’est pas encore validé.", new { reason = "statement-draft" });
        if (statement.Status == CourierStatementStatus.PAID) throw BusinessException.Conflict("Ce relevé est déjà payé.", new { reason = "statement-already-paid" });
        await ReturnRows.AssertAgencyReachableAsync(db, caller, statement.AgencyId, cancellationToken);
        if (!string.IsNullOrEmpty(input.BankAccountId) && !await db.BankAccounts.AnyAsync(a => a.Id == input.BankAccountId && a.DeletedAt == null, cancellationToken))
        {
            throw BusinessException.NotFound("Compte bancaire introuvable.");
        }
        var proof = input.Proof is null ? null : await storage.PutAsync($"cheques/{caller.CompanyId}", Convert.FromBase64String(input.Proof.Content!), input.Proof.MimeType!, cancellationToken);
        var row = await db.CourierStatements.FirstAsync(s => s.Id == statement.Id, cancellationToken);
        row.Status = CourierStatementStatus.PAID;
        row.PaidAt = DateTime.UtcNow;
        row.PaidById = caller.UserId;
        row.PaymentMethod = Enum.Parse<PaymentMethod>((input.Method ?? "cash").ToUpperInvariant());
        row.BankAccountId = ReturnRows.Text(input.BankAccountId);
        row.ChequeNumber = ReturnRows.Text(input.ChequeNumber);
        row.ChequeBank = ReturnRows.Text(input.ChequeBank);
        row.Reference = ReturnRows.Text(input.Reference);
        row.ChequeDate = string.IsNullOrEmpty(input.ChequeDate) ? null : PettyCashDays.Parse(input.ChequeDate);
        row.UpdatedAt = DateTime.UtcNow;
        if (proof is not null) { row.ProofName = input.Proof!.Name; row.ProofMimeType = input.Proof.MimeType; row.ProofPath = proof.Key; }
        await db.SaveChangesAsync(cancellationToken);
        return await CourierStatements.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
