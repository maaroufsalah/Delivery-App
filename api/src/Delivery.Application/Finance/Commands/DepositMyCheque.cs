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

/// <summary>`courierChequeSchema` : le livreur photographie le chèque de son dernier relevé réglé par chèque.</summary>
public sealed record DepositMyChequeCommand : IRequest<CourierStatementDetailDto>
{
    public string? ChequeNumber { get; init => field = Zod.Trim(value); }
    public string? ChequeBank { get; init => field = Zod.Trim(value); }
    public string? ChequeDate { get; init => field = Zod.Trim(value); }
    public double? Amount { get; init; }
    public ProofFileDto? Proof { get; init; }
}

public sealed class DepositMyChequeValidator : ZodValidator<DepositMyChequeCommand>
{
    private static readonly string[] Images = ["image/jpeg", "image/png", "image/webp"];

    public DepositMyChequeValidator()
    {
        RuleFor(x => x.ChequeNumber).Required().MinLength(1, "required").MaxLength(40);
        RuleFor(x => x.ChequeBank).MaxLength(80);
        RuleFor(x => x.ChequeDate).Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("invalid-date").When(x => !string.IsNullOrEmpty(x.ChequeDate));
        RuleFor(x => x.Amount).Must(v => v is null || v.Value >= 0).WithMessage("negative").Must(v => v is null || v.Value <= 1_000_000).WithMessage("amount-too-large");
        RuleFor(x => x.Proof).Required("object");
        RuleFor(x => x.Proof!.Name).Required().MinLength(1, "required").MaxLength(120).OverridePropertyName("proof.name").When(x => x.Proof is not null);
        RuleFor(x => x.Proof!.MimeType).Must(v => v is not null && Images.Contains(v)).WithMessage("unsupported-file-type").OverridePropertyName("proof.mimeType").When(x => x.Proof is not null);
        RuleFor(x => x.Proof!.Content).Required().MinLength(1, "required").OverridePropertyName("proof.content").When(x => x.Proof is not null);
    }
}

internal sealed class DepositMyChequeHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage) : IRequestHandler<DepositMyChequeCommand, CourierStatementDetailDto>
{
    public async Task<CourierStatementDetailDto> Handle(DepositMyChequeCommand input, CancellationToken cancellationToken)
    {
        var courierId = caller.CourierId ?? throw BusinessException.Forbidden("Cet espace est réservé aux livreurs.", new { reason = "not-a-courier" });
        var statement = await db.CourierStatements.Where(s => s.CourierId == courierId && s.Status == CourierStatementStatus.PAID && s.PaymentMethod == PaymentMethod.CHECK)
                            .OrderByDescending(s => s.PaidAt).FirstOrDefaultAsync(cancellationToken)
                        ?? throw BusinessException.NotFound("Aucun relevé réglé par chèque à documenter. Demandez à votre agence.", new { reason = "no-cheque-statement" });
        var previous = statement.ProofPath;
        var stored = await storage.PutAsync($"cheques/{caller.CompanyId}", Convert.FromBase64String(input.Proof!.Content!), input.Proof.MimeType!, cancellationToken);
        statement.ProofName = input.Proof.Name;
        statement.ProofMimeType = input.Proof.MimeType;
        statement.ProofPath = stored.Key;
        statement.ChequeNumber ??= input.ChequeNumber;
        statement.ChequeBank ??= ReturnRows.Text(input.ChequeBank);
        statement.ChequeDate ??= string.IsNullOrEmpty(input.ChequeDate) ? null : PettyCashDays.Parse(input.ChequeDate);
        statement.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        if (previous is not null) await storage.RemoveAsync(previous, cancellationToken);
        return await CourierStatements.DetailAsync(db, caller, statement.Code, cancellationToken);
    }
}
