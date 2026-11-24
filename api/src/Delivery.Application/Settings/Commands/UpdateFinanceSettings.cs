using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.Settings.Dtos;
using Delivery.Application.Settings.Queries;
using Delivery.Domain.Common;
using Delivery.Domain.Pricing;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Settings.Commands;

/// <summary>`PATCH /settings/finance` : TVA, préfixes de numérotation, frais, paie des livreurs.</summary>
public sealed record UpdateFinanceSettingsCommand : IRequest<FinanceSettingsDto>
{
    public double? VatRate { get; init; }

    public double? ProVatRate { get; init; }

    public string? InvoicePrefix { get; init => field = value?.Trim().ToUpperInvariant(); }

    public string? RemittancePrefix { get; init => field = value?.Trim().ToUpperInvariant(); }

    public string? StatementPrefix { get; init => field = value?.Trim().ToUpperInvariant(); }

    public string? CodPayoutPrefix { get; init => field = value?.Trim().ToUpperInvariant(); }

    public bool? NumberingYearlyReset { get; init; }

    public double? FailureFeeSameCity { get; init; }

    public double? FailureFeeCrossCity { get; init; }

    public double? PickupFee { get; init; }

    public double? FreePickupThreshold { get; init; }

    public double? CourierReturnFee { get; init; }

    public double? CourierFailedCommission { get; init; }

    public string? CourierPayPeriod { get; init; }

    public string? InvoiceFooter { get; init => field = Zod.Trim(value); }

    public string? CodPayoutFooter { get; init => field = Zod.Trim(value); }

    public string? DefaultBankAccountId { get; init => field = Zod.Trim(value); }

    public double? AuditRetentionDays { get; init; }
}

public sealed class UpdateFinanceSettingsValidator : ZodValidator<UpdateFinanceSettingsCommand>
{
    private static readonly Regex Prefix = new("^[A-Z0-9]+$", RegexOptions.Compiled);
    private static readonly int[] ProVatRates = [2000, 1400, 1000, 700, 0];
    private static readonly string[] PayPeriods = ["daily", "weekly", "monthly"];

    public UpdateFinanceSettingsValidator()
    {
        RuleFor(x => x.VatRate).Required("number").Min(0, "negative").Max(100, "too-large");
        RuleFor(x => x.ProVatRate).Must(r => r is null || ProVatRates.Contains((int)Money.MadToCents(r.Value))).WithMessage("invalid");
        RuleFor(x => x.InvoicePrefix).Required().MinLength(1, "required").MaxLength(5, "too-large").Pattern(Prefix, "invalid-prefix");
        RuleFor(x => x.RemittancePrefix).Required().MinLength(1, "required").MaxLength(5, "too-large").Pattern(Prefix, "invalid-prefix");
        RuleFor(x => x.StatementPrefix).Required().MinLength(1, "required").MaxLength(5, "too-large").Pattern(Prefix, "invalid-prefix");
        RuleFor(x => x.CodPayoutPrefix).Required().MinLength(1, "required").MaxLength(5, "too-large").Pattern(Prefix, "invalid-prefix");
        RuleFor(x => x.NumberingYearlyReset).Required("boolean");
        RuleFor(x => x.FailureFeeSameCity).MadAmount();
        RuleFor(x => x.FailureFeeCrossCity).MadAmount();
        RuleFor(x => x.PickupFee).MadAmount();
        RuleFor(x => x.FreePickupThreshold).Required("number").Int().Min(0, "negative").Max(1000, "too-large");
        RuleFor(x => x.CourierReturnFee).MadAmount();
        RuleFor(x => x.CourierFailedCommission).MadAmount();
        RuleFor(x => x.CourierPayPeriod).Must(p => p is not null && PayPeriods.Contains(p)).WithMessage(Zod.InvalidOption(PayPeriods));
        RuleFor(x => x.InvoiceFooter).Required().MaxLength(300, "too-large");
        RuleFor(x => x.CodPayoutFooter).Required().MaxLength(300, "too-large");
        RuleFor(x => x.AuditRetentionDays).Required("number").Int().Min(0, "negative").Max(3650, "too-large");
    }
}

internal sealed class UpdateFinanceSettingsHandler(IAppDbContext db) : IRequestHandler<UpdateFinanceSettingsCommand, FinanceSettingsDto>
{
    public async Task<FinanceSettingsDto> Handle(UpdateFinanceSettingsCommand input, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(input.DefaultBankAccountId)
            && !await db.BankAccounts.AnyAsync(a => a.Id == input.DefaultBankAccountId && a.DeletedAt == null, cancellationToken))
        {
            throw BusinessException.Validation("Compte bancaire introuvable.", new { field = "defaultBankAccountId" });
        }
        var p = PricingPolicy.Default;
        var company = await db.CompanyAsync(cancellationToken);
        static JsonNode Cents(double? mad) => Money.MadToCents(mad!.Value);
        var writes = new List<(string, JsonNode?, JsonNode?)>
        {
            (SettingKeys.VatRate, Cents(input.VatRate), 0),
            (SettingKeys.InvoicePrefix, input.InvoicePrefix, "FA"),
            (SettingKeys.RemittancePrefix, input.RemittancePrefix, "RF"),
            (SettingKeys.StatementPrefix, input.StatementPrefix, "PAI"),
            (SettingKeys.NumberingYearlyReset, input.NumberingYearlyReset, false),
            (SettingKeys.FailureFeeSameCity, Cents(input.FailureFeeSameCity), p.FailureFeeSameCity),
            (SettingKeys.FailureFeeCrossCity, Cents(input.FailureFeeCrossCity), p.FailureFeeCrossCity),
            (SettingKeys.PickupFee, Cents(input.PickupFee), p.PickupFee),
            (SettingKeys.FreePickupThreshold, input.FreePickupThreshold, p.FreePickupThreshold),
            (SettingKeys.CourierReturnFee, Cents(input.CourierReturnFee), 0),
            (SettingKeys.CourierFailedCommission, Cents(input.CourierFailedCommission), 0),
            (SettingKeys.CourierPayPeriod, input.CourierPayPeriod, "monthly"),
            (SettingKeys.InvoiceFooter, input.InvoiceFooter, ""),
            (SettingKeys.CodPayoutFooter, input.CodPayoutFooter, ""),
            (SettingKeys.CodPayoutPrefix, input.CodPayoutPrefix, "BV"),
            (SettingKeys.DefaultBankAccount, input.DefaultBankAccountId ?? string.Empty, ""),
            (SettingKeys.AuditRetentionDays, input.AuditRetentionDays, 0),
        };
        if (input.ProVatRate is not null) writes.Add((SettingKeys.ProVatRate, Cents(input.ProVatRate), 2000));
        await db.WriteChangedAsync(company.Id, writes, cancellationToken);
        return await GetFinanceSettingsHandler.BuildAsync(db, cancellationToken);
    }
}
