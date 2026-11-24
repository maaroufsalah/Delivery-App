using System.Text.Json;
using System.Text.Json.Nodes;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Settings.Dtos;
using MediatR;

namespace Delivery.Application.Settings.Queries;

/// <summary>`GET /settings/finance` : centimes stockés, MAD affichés.</summary>
public sealed record GetFinanceSettingsQuery : IRequest<FinanceSettingsDto>;

internal sealed class GetFinanceSettingsHandler(IAppDbContext db) : IRequestHandler<GetFinanceSettingsQuery, FinanceSettingsDto>
{
    public Task<FinanceSettingsDto> Handle(GetFinanceSettingsQuery request, CancellationToken cancellationToken) => BuildAsync(db, cancellationToken);

    internal static async Task<FinanceSettingsDto> BuildAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var all = await db.AllSettingsAsync(null, cancellationToken);
        var policy = await db.PricingPolicyAsync(cancellationToken);
        double Number(string key, double fallback) => all.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : fallback;
        string Text(string key, string fallback) => all.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : fallback;
        bool Flag(string key, bool fallback) => all.TryGetValue(key, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : fallback;
        static double Mad(double cents) => cents / 100;
        var account = Text(SettingKeys.DefaultBankAccount, string.Empty);
        return new FinanceSettingsDto(
            Mad(Number(SettingKeys.VatRate, 0)),
            Mad(Number(SettingKeys.ProVatRate, 2000)),
            Text(SettingKeys.InvoicePrefix, "FA"),
            Text(SettingKeys.RemittancePrefix, "RF"),
            Text(SettingKeys.StatementPrefix, "PAI"),
            Text(SettingKeys.CodPayoutPrefix, "BV"),
            Flag(SettingKeys.NumberingYearlyReset, false),
            Mad(policy.FailureFeeSameCity),
            Mad(policy.FailureFeeCrossCity),
            Mad(policy.PickupFee),
            policy.FreePickupThreshold,
            Mad(Number(SettingKeys.CourierReturnFee, 0)),
            Mad(Number(SettingKeys.CourierFailedCommission, 0)),
            Text(SettingKeys.CourierPayPeriod, "monthly"),
            Text(SettingKeys.InvoiceFooter, string.Empty),
            Text(SettingKeys.CodPayoutFooter, string.Empty),
            account.Length == 0 ? null : account,
            Number(SettingKeys.AuditRetentionDays, 0),
            0);
    }
}
