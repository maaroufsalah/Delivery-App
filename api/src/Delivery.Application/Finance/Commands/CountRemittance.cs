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
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
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

/// <summary>`remittanceCountSchema` : le montant remis se saisit ; un écart demande un motif.</summary>
public sealed record CountRemittanceCommand : IRequest<RemittanceDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public double? DeclaredAmount { get; init; }
    public string? VarianceReason { get; init => field = Zod.Trim(value); }
    public double? ExpectedNet { get; init; }
}

public sealed class CountRemittanceValidator : ZodValidator<CountRemittanceCommand>
{
    public CountRemittanceValidator()
    {
        RuleFor(x => x.DeclaredAmount).MadAmount0();
        RuleFor(x => x.ExpectedNet).MadAmount0();
        RuleFor(x => x.VarianceReason).MaxLength(300);
    }

    protected override void Refine(CountRemittanceCommand value, Action<string, string> issue)
    {
        if (value.DeclaredAmount is not null && value.ExpectedNet is not null && value.DeclaredAmount != value.ExpectedNet && string.IsNullOrEmpty(value.VarianceReason))
        {
            issue(ValidationPaths.Raw + "varianceReason", "variance-needs-reason");
        }
    }
}

internal sealed class CountRemittanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CountRemittanceCommand, RemittanceDetailDto>
{
    public async Task<RemittanceDetailDto> Handle(CountRemittanceCommand input, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not null) throw BusinessException.Forbidden("Seule l’agence compte un retour de fonds.", new { reason = "counting-is-agency-only" });
        var remittance = await Remittances.RequireDraftAsync(db, caller, input.Code, cancellationToken);
        var declared = (long)Math.Round(input.DeclaredAmount!.Value * 100, MidpointRounding.AwayFromZero);
        var lines = await db.CashRemittanceParcels.AsNoTracking().Where(l => l.RemittanceId == remittance.Id).Select(l => new { l.Amount, l.Cheque, l.Commission }).ToListAsync(cancellationToken);
        var net = Remittances.Sum(lines.Select(l => (l.Amount, l.Cheque, l.Commission))).Net;
        if (declared != net && string.IsNullOrEmpty(input.VarianceReason))
        {
            throw BusinessException.Validation("Un écart entre le montant remis et le net attendu demande un motif.", new { field = "varianceReason" });
        }
        var amount = Money.FromCents(declared);
        var reason = declared == net ? null : input.VarianceReason;
        await db.CashRemittances.Where(r => r.Id == remittance.Id).ExecuteUpdateAsync(set => set.SetProperty(r => r.DeclaredAmount, amount).SetProperty(r => r.VarianceReason, reason)
            .SetProperty(r => r.Amount, amount).SetProperty(r => r.UpdatedAt, DateTime.UtcNow), cancellationToken);
        return await Remittances.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
